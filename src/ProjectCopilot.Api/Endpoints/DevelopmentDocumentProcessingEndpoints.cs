using System.Security.Claims;
using FluentValidation;
using FluentValidation.Results;
using ProjectCopilot.Api.Authorization;
using ProjectCopilot.Application.Documents;
using ProjectCopilot.Application.Documents.TransitionDocumentProcessingStatus;

namespace ProjectCopilot.Api.Endpoints;

/// <summary>
/// Temporary Slice 2.2 verification plumbing: lets a project member drive a document through its
/// processing states by hand, so the state machine can be observed through the API.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Registered only when the host is running in Development</strong> — see the
/// <c>app.Environment.IsDevelopment()</c> gate in <c>Program.cs</c>. Outside Development the
/// route is never added to the routing table at all, so it cannot be reached, authorized or
/// probed; it is absent rather than hidden.
/// </para>
/// <para>
/// It exists because Slice 2.2 deliberately contains no real processing, so without it there
/// would be no way to observe a transition, and shipping a production endpoint that lets any
/// member declare a document "Ready" with no processing behind it would be a feature that only
/// looks real. This route does not implement the state machine: it maps an action word onto a
/// target status and calls
/// <see cref="TransitionDocumentProcessingStatusHandler"/>, which owns the rules and the
/// authorization, exactly as any other caller would.
/// </para>
/// <para>
/// Slice 2.3 is expected to call that same handler for real, from actual extraction code. Once
/// it does, whether this Development-only route should still exist is worth revisiting — that
/// decision belongs to that slice, not this one.
/// </para>
/// </remarks>
public static class DevelopmentDocumentProcessingEndpoints
{
    /// <summary>Starts processing: Uploaded → Processing.</summary>
    private const string StartAction = "Start";

    /// <summary>Reports success: Processing → Ready.</summary>
    private const string SucceedAction = "Succeed";

    /// <summary>Reports failure: Processing → Failed.</summary>
    private const string FailAction = "Fail";

    public static IEndpointRouteBuilder MapDevelopmentDocumentProcessingEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/documents");

        // Authenticated like every other documents route. The handler performs the authoritative
        // load-and-authorize against the document's own project — the same check the production
        // endpoints rely on — so being Development-only buys this route no shortcut.
        group.MapPost("/{id:guid}/processing", async (
            Guid id,
            DocumentProcessingActionRequest request,
            TransitionDocumentProcessingStatusHandler handler,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var command = new TransitionDocumentProcessingStatusCommand(
                id,
                ResolveTargetStatus(request.Action));

            var document = await handler.HandleAsync(
                command,
                user.GetUserId(),
                cancellationToken);

            return Results.Ok(DocumentResponse.From(document));
        }).RequireAuthorization();

        return app;
    }

    /// <summary>
    /// Maps the request's action word onto the status to move to. An unrecognised action is a
    /// malformed request (400), not a rejected transition (409) — the caller asked for something
    /// that does not exist rather than something that is not currently allowed.
    /// </summary>
    private static string ResolveTargetStatus(string? action) => action switch
    {
        not null when action.Equals(StartAction, StringComparison.OrdinalIgnoreCase) =>
            DocumentProcessingStatuses.Processing,
        not null when action.Equals(SucceedAction, StringComparison.OrdinalIgnoreCase) =>
            DocumentProcessingStatuses.Ready,
        not null when action.Equals(FailAction, StringComparison.OrdinalIgnoreCase) =>
            DocumentProcessingStatuses.Failed,
        _ => throw new ValidationException(
        [
            new ValidationFailure(
                nameof(DocumentProcessingActionRequest.Action),
                $"Action must be one of: {StartAction}, {SucceedAction}, {FailAction}.")
        ])
    };
}

/// <summary>
/// Body of the Development-only processing route. Carries an action word rather than a status so
/// a caller cannot name an arbitrary status to jump to.
/// </summary>
public sealed record DocumentProcessingActionRequest(string? Action);
