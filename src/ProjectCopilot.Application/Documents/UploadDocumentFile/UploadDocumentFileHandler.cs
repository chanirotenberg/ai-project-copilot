using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using ProjectCopilot.Application.Abstractions;
using ProjectCopilot.Application.Common.Exceptions;
using ProjectCopilot.Domain.Entities;

namespace ProjectCopilot.Application.Documents.UploadDocumentFile;

/// <summary>
/// Attaches one uploaded file to an existing document.
/// </summary>
/// <remarks>
/// Ordering is a security property, not a style choice:
/// <list type="number">
/// <item>load the document and authorize against <em>its</em> project (never a client-supplied
/// project id);</item>
/// <item>validate size, file type and actual content — all read-only;</item>
/// <item>reject a re-upload — still read-only;</item>
/// <item>write the file to storage;</item>
/// <item>only then mutate and persist the row.</item>
/// </list>
/// Nothing before step 4 mutates anything, so every rejection leaves the document exactly as it
/// was. If step 5 fails after step 4 succeeded, the written file is removed again so no document
/// row can end up pointing at a file that is not there, and vice versa.
///
/// Step 3 cannot settle a concurrent re-upload on its own, because two requests can both read a
/// file-less document before either commits. The document row's optimistic concurrency token is
/// what decides that race at step 5: the losing write fails, its file is compensated away, and
/// it is reported as the same 409 as an ordinary re-upload.
/// </remarks>
public sealed class UploadDocumentFileHandler
{
    /// <summary>
    /// Shared by both conflict paths — the pre-check and the lost concurrency race — so the two
    /// are indistinguishable to the caller.
    /// </summary>
    public const string DocumentAlreadyHasFileMessage =
        "This document already has an uploaded file.";

    private readonly IDocumentRepository _documentRepository;
    private readonly IProjectMembershipService _membershipService;
    private readonly IDocumentFileStorage _fileStorage;
    private readonly IValidator<UploadDocumentFileCommand> _validator;
    private readonly ILogger<UploadDocumentFileHandler> _logger;

    public UploadDocumentFileHandler(
        IDocumentRepository documentRepository,
        IProjectMembershipService membershipService,
        IDocumentFileStorage fileStorage,
        IValidator<UploadDocumentFileCommand> validator,
        ILogger<UploadDocumentFileHandler> logger)
    {
        _documentRepository = documentRepository;
        _membershipService = membershipService;
        _fileStorage = fileStorage;
        _validator = validator;
        _logger = logger;
    }

    public async Task<DocumentMetadata> HandleAsync(
        UploadDocumentFileCommand command,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // 1. Load + authorize before anything touches the uploaded payload. The project comes
        //    from the stored document, so there is no project id in the request to tamper with.
        var document = await _documentRepository.GetByIdAsync(
            command.DocumentId,
            cancellationToken);

        if (document is null)
        {
            // Must stay textually identical to the membership-check 404 below — same
            // indistinguishability rationale as the other document endpoints.
            throw NotFoundException.HiddenResource();
        }

        await _membershipService.EnsureMemberAsync(
            document.ProjectId,
            userId,
            cancellationToken);

        // 2. Validation (read-only).
        await _validator.ValidateAndThrowAsync(command, cancellationToken);

        if (!command.Content.CanSeek)
        {
            // The content inspection below reads the head and then rewinds, so a non-seekable
            // stream cannot be inspected. Fail explicitly rather than skipping the check.
            throw new InvalidOperationException(
                "Uploaded document content must be provided as a seekable stream.");
        }

        // The stream length is the ground truth for how many bytes were actually bound; a
        // mismatch against the reported size means the two layers disagree, so fail closed
        // instead of trusting either.
        if (command.Content.Length != command.SizeBytes)
        {
            throw ValidationFailureFor("The uploaded file size could not be determined reliably.");
        }

        // The extension is guaranteed allow-listed by the validator; this is the allow-list's own
        // literal, which is the only extension that ever reaches the storage file name.
        var extension = DocumentFileTypes.ResolveAllowedExtension(command.FileName)
            ?? throw ValidationFailureFor("File type is not supported.");

        var head = await ReadHeadAsync(command.Content, cancellationToken);

        if (!DocumentFileContentInspector.MatchesExtension(extension, head))
        {
            throw ValidationFailureFor(
                "File content does not match its file type and was rejected.");
        }

        // 3. Re-upload conflict (read-only). This slice intentionally has no replace/versioning.
        //    This check alone is not sufficient under concurrency — two requests can both read a
        //    file-less document and both get past here. The row's concurrency token is what
        //    actually settles that race, at step 5.
        if (document.StorageFileName is not null)
        {
            throw new ConflictException(DocumentAlreadyHasFileMessage);
        }

        // 4. Write to storage. The name is entirely server-generated.
        var storageFileName = Guid.NewGuid().ToString("N") + extension;

        command.Content.Position = 0;
        await _fileStorage.SaveAsync(command.Content, storageFileName, cancellationToken);

        // 5. Persist metadata. On failure, undo step 4 so no file is left without a row.
        try
        {
            document.OriginalFileName = command.FileName;
            document.ContentType = DocumentFileTypes.CanonicalContentType(extension);
            document.SizeBytes = command.SizeBytes;
            document.StorageFileName = storageFileName;

            // The initial processing status is set in this same mutation, committed by the same
            // single save below: a document can never be observed as having a file but no
            // processing status. Every later move out of Uploaded goes through
            // TransitionDocumentProcessingStatusHandler, which owns the transition rules.
            document.Status = DocumentProcessingStatuses.Uploaded;

            document.UpdatedAt = DateTime.UtcNow;

            await _documentRepository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            // Losing the concurrency race is an expected outcome of two clients uploading at
            // once, not a fault, so it is not logged as an error.
            var lostConcurrencyRace = exception is ConcurrencyConflictException;

            if (lostConcurrencyRace)
            {
                _logger.LogWarning(
                    exception,
                    "A concurrent upload won the race for document {DocumentId}; removing the file this request wrote.",
                    document.Id);
            }
            else
            {
                _logger.LogError(
                    exception,
                    "Failed to persist uploaded file metadata for document {DocumentId}; removing the stored file.",
                    document.Id);
            }

            // Compensation must run even when the request was cancelled, otherwise cancellation
            // would be the one path that leaks an orphaned file.
            try
            {
                await _fileStorage.DeleteAsync(storageFileName, CancellationToken.None);
            }
            catch (Exception cleanupException)
            {
                _logger.LogError(
                    cleanupException,
                    "Failed to remove the stored file after a metadata persistence failure for document {DocumentId}. An orphaned file remains in document storage.",
                    document.Id);
            }

            if (lostConcurrencyRace)
            {
                // Another upload for this document committed first, so this document does now
                // have a file. Reported identically to the pre-check conflict above, so a client
                // cannot tell whether it lost a race or simply arrived second.
                throw new ConflictException(DocumentAlreadyHasFileMessage);
            }

            throw;
        }

        return document;
    }

    private static async Task<byte[]> ReadHeadAsync(
        Stream content,
        CancellationToken cancellationToken)
    {
        content.Position = 0;

        var buffer = new byte[DocumentFileContentInspector.HeadByteCount];

        var read = await content.ReadAtLeastAsync(
            buffer,
            buffer.Length,
            throwOnEndOfStream: false,
            cancellationToken);

        content.Position = 0;

        return read == buffer.Length ? buffer : buffer[..read];
    }

    /// <summary>
    /// Produces the same structured 400 body as any other FluentValidation failure, so file
    /// rejections are indistinguishable in shape from ordinary validation errors.
    /// </summary>
    private static ValidationException ValidationFailureFor(string message) =>
        new([new ValidationFailure(nameof(UploadDocumentFileCommand.FileName), message)]);
}
