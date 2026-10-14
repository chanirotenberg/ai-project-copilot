using System.Security.Claims;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using ProjectCopilot.Api.Authorization;
using ProjectCopilot.Application.Abstractions;
using ProjectCopilot.Application.Common.Exceptions;
using ProjectCopilot.Application.Documents.CreateDocument;
using ProjectCopilot.Application.Documents.UpdateDocument;
using ProjectCopilot.Application.Documents.UploadDocumentFile;
using ProjectCopilot.Domain.Entities;
using ProjectCopilot.Infrastructure.Storage;

namespace ProjectCopilot.Api.Endpoints;

public static class DocumentsEndpoints
{
    /// <summary>
    /// Name of the single multipart form field carrying the uploaded file.
    /// </summary>
    private const string UploadFormFieldName = "file";

    /// <summary>
    /// Headroom above the configured file-size limit for the multipart envelope itself
    /// (boundary markers and part headers), so a file exactly at the limit still fits.
    /// </summary>
    private const long MultipartEnvelopeAllowanceBytes = 8 * 1024;

    /// <summary>
    /// Reported when a delete loses an optimistic-concurrency race against another writer.
    /// Deliberately a 409, matching <see cref="UpdateDocumentHandler.ConcurrentlyModifiedMessage"/>'s
    /// semantics, rather than a 404 — the caller's request did not take effect, which is a
    /// different outcome from "this is already gone".
    /// </summary>
    public const string ConcurrentlyModifiedMessage =
        "This document was modified or removed by someone else. Reload it and try again.";

    public static IEndpointRouteBuilder MapDocumentsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/documents");

        group.MapGet("/", async (
            Guid projectId,
            IDocumentRepository repository,
            IProjectMembershipService membershipService,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            await membershipService.EnsureMemberAsync(
                projectId,
                user.GetUserId(),
                cancellationToken);

            var documents = await repository.GetByProjectIdAsync(
                projectId,
                cancellationToken);

            return Results.Ok(documents.Select(DocumentResponse.From).ToList());
        }).RequireAuthorization();

        group.MapGet("/{id:guid}", async (
            Guid id,
            IDocumentRepository repository,
            IProjectMembershipService membershipService,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var document = await repository.GetByIdAsync(
                id,
                cancellationToken);

            if (document is null)
            {
                // Must stay textually identical to the membership-check 404 below (and to the
                // generic NotFoundException.HiddenResource() message) — see TasksEndpoints for
                // the same pattern/rationale.
                throw NotFoundException.HiddenResource();
            }

            await membershipService.EnsureMemberAsync(
                document.ProjectId,
                user.GetUserId(),
                cancellationToken);

            return Results.Ok(DocumentResponse.From(document));
        }).RequireAuthorization();

        group.MapPost("/", async (
            CreateDocumentCommand command,
            CreateDocumentHandler handler,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var document = await handler.HandleAsync(command, user.GetUserId(), cancellationToken);

            return Results.Created(
                $"/api/v1/documents/{document.Id}",
                DocumentResponse.From(document));
        }).RequireAuthorization();

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateDocumentRequest request,
            UpdateDocumentHandler handler,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateDocumentCommand(
                id,
                request.Title,
                request.Description);

            var document = await handler.HandleAsync(
                command,
                user.GetUserId(),
                cancellationToken);

            return Results.Ok(DocumentResponse.From(document));
        }).RequireAuthorization();

        // Attaches one file to an existing document. The route carries only the document id —
        // the owning project is resolved server-side from the stored document, so there is no
        // project identifier in this request for a caller to point somewhere else.
        group.MapPost("/{id:guid}/file", async (
            Guid id,
            HttpContext httpContext,
            IDocumentRepository repository,
            IProjectMembershipService membershipService,
            UploadDocumentFileHandler handler,
            IOptions<DocumentUploadOptions> uploadOptions,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var userId = user.GetUserId();

            // Transport-level ceiling, applied to this route only — no global Kestrel change.
            // Set before the body is touched so an oversized upload is cut off by the server
            // instead of being buffered in full. Exceeding it surfaces as a 413.
            var maxBodySizeFeature = httpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();

            if (maxBodySizeFeature is { IsReadOnly: false })
            {
                maxBodySizeFeature.MaxRequestBodySize =
                    uploadOptions.Value.MaxFileSizeBytes + MultipartEnvelopeAllowanceBytes;
            }

            // Authorize before reading the multipart body, so a caller with no access to this
            // document cannot make the server buffer a payload at all. UploadDocumentFileHandler
            // repeats the load-and-authorize independently and is the authoritative check — this
            // one is purely an early-out and must never be the only gate.
            var document = await repository.GetByIdAsync(id, cancellationToken);

            if (document is null)
            {
                // Must stay textually identical to the membership-check 404 below — same
                // indistinguishability rationale as the GET endpoint above.
                throw NotFoundException.HiddenResource();
            }

            await membershipService.EnsureMemberAsync(
                document.ProjectId,
                userId,
                cancellationToken);

            if (!httpContext.Request.HasFormContentType)
            {
                throw UploadRequestRejection(
                    "The request must be sent as multipart/form-data.");
            }

            var form = await httpContext.Request.ReadFormAsync(cancellationToken);

            if (form.Files.Count != 1)
            {
                throw UploadRequestRejection(
                    $"Exactly one file must be uploaded in the '{UploadFormFieldName}' form field.");
            }

            var file = form.Files[0];

            if (!string.Equals(file.Name, UploadFormFieldName, StringComparison.Ordinal))
            {
                throw UploadRequestRejection(
                    $"The uploaded file must be sent in the '{UploadFormFieldName}' form field.");
            }

            await using var content = file.OpenReadStream();

            var command = new UploadDocumentFileCommand(
                id,
                file.FileName,
                file.Length,
                content);

            var updated = await handler.HandleAsync(command, userId, cancellationToken);

            return Results.Ok(DocumentResponse.From(updated));
        }).RequireAuthorization();

        // Deletion is deliberately allowed in every processing status, including Processing:
        // nothing is actually processing documents yet, so there is nothing to cancel. Once
        // Slice 2.3 introduces a real processor, revisit delete-versus-processing concurrency
        // (cancellation, or refusing to delete mid-processing) — do not assume this stays correct.
        group.MapDelete("/{id:guid}", async (
            Guid id,
            IDocumentRepository repository,
            IProjectMembershipService membershipService,
            IDocumentFileStorage fileStorage,
            ILoggerFactory loggerFactory,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var document = await repository.GetByIdAsync(
                id,
                cancellationToken);

            if (document is null)
            {
                // Must stay textually identical to the membership-check 404 below — same
                // indistinguishability rationale as the GET endpoint above.
                throw NotFoundException.HiddenResource();
            }

            await membershipService.EnsureMemberAsync(
                document.ProjectId,
                user.GetUserId(),
                cancellationToken);

            var storageFileName = document.StorageFileName;

            // The row goes first: once it is gone, no document can point at a missing file. The
            // reverse order would risk a document referencing a file that had been removed.
            repository.Delete(document);

            try
            {
                await repository.SaveChangesAsync(cancellationToken);
            }
            catch (ConcurrencyConflictException)
            {
                // EF includes the row's concurrency token in a DELETE's WHERE clause too, so a
                // racing delete or edit makes this one match no rows. Reported as 409 for
                // consistency with UpdateDocumentHandler rather than as an unexpected failure.
                //
                // The stored file is deliberately left untouched here: this request did not
                // delete the row, so removing the file could strand surviving metadata pointing
                // at nothing. Whoever actually deletes the row performs its cleanup.
                throw new ConflictException(ConcurrentlyModifiedMessage);
            }

            if (storageFileName is not null)
            {
                try
                {
                    // CancellationToken.None: the row is already deleted, so cleanup must not be
                    // skipped just because the client disconnected.
                    await fileStorage.DeleteAsync(storageFileName, CancellationToken.None);
                }
                catch (Exception exception)
                {
                    // The client-visible outcome is the committed row deletion, which succeeded.
                    // A failed cleanup leaves an orphaned file for operators to reclaim; it must
                    // not fail the request or resurrect the metadata. The storage file name is
                    // deliberately not logged.
                    loggerFactory
                        .CreateLogger(typeof(DocumentsEndpoints).FullName!)
                        .LogError(
                        exception,
                        "Failed to remove the stored file for deleted document {DocumentId}. An orphaned file may remain in document storage.",
                        id);
                }
            }

            return Results.NoContent();
        }).RequireAuthorization();

        return app;
    }

    /// <summary>
    /// Rejects a malformed upload request using the same structured 400 body as any other
    /// validation failure, instead of inventing a second error shape.
    /// </summary>
    private static ValidationException UploadRequestRejection(string message) =>
        new([new ValidationFailure(nameof(UploadDocumentFileCommand.FileName), message)]);
}

public sealed record UpdateDocumentRequest(
    string Title,
    string? Description);

/// <summary>
/// API representation of a document.
/// </summary>
/// <remarks>
/// This projection exists so that <see cref="DocumentMetadata.StorageFileName"/> — the internal
/// on-disk name — can never leave the API layer. Every document-returning endpoint maps through
/// here rather than serialising the entity directly; adding a new field to the entity must not
/// silently publish it.
/// </remarks>
public sealed record DocumentResponse(
    Guid Id,
    Guid ProjectId,
    string Title,
    string? Description,
    Guid CreatedByUserId,
    string? OriginalFileName,
    string? ContentType,
    long? SizeBytes,
    string? Status,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static DocumentResponse From(DocumentMetadata document) => new(
        document.Id,
        document.ProjectId,
        document.Title,
        document.Description,
        document.CreatedByUserId,
        document.OriginalFileName,
        document.ContentType,
        document.SizeBytes,
        document.Status,
        document.CreatedAt,
        document.UpdatedAt);
}
