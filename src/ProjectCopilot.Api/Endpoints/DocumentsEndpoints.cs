using System.Security.Claims;
using ProjectCopilot.Api.Authorization;
using ProjectCopilot.Application.Abstractions;
using ProjectCopilot.Application.Common.Exceptions;
using ProjectCopilot.Application.Documents.CreateDocument;
using ProjectCopilot.Application.Documents.UpdateDocument;

namespace ProjectCopilot.Api.Endpoints;

public static class DocumentsEndpoints
{
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

            return Results.Ok(documents);
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
                throw new NotFoundException($"Document with id '{id}' was not found.");
            }

            await membershipService.EnsureMemberAsync(
                document.ProjectId,
                user.GetUserId(),
                cancellationToken);

            return Results.Ok(document);
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
                document);
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

            return Results.Ok(document);
        }).RequireAuthorization();

        group.MapDelete("/{id:guid}", async (
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
                throw new NotFoundException($"Document with id '{id}' was not found.");
            }

            await membershipService.EnsureMemberAsync(
                document.ProjectId,
                user.GetUserId(),
                cancellationToken);

            repository.Delete(document);
            await repository.SaveChangesAsync(cancellationToken);

            return Results.NoContent();
        }).RequireAuthorization();

        return app;
    }
}

public sealed record UpdateDocumentRequest(
    string Title,
    string? Description);
