using FluentValidation;
using ProjectCopilot.Application.Abstractions;
using ProjectCopilot.Application.Common.Exceptions;
using ProjectCopilot.Application.Documents.UpdateDocument;
using ProjectCopilot.Domain.Entities;

namespace ProjectCopilot.Application.Documents.TransitionDocumentProcessingStatus;

/// <summary>
/// The single place a document's processing status may change. Holds the legal-transition table
/// and enforces it.
/// </summary>
/// <remarks>
/// Ordering mirrors every other document handler, and is a security property rather than style:
/// load the document, authorize against <em>its</em> project (never a client-supplied project
/// id), and only then apply the rule and persist.
///
/// Nothing here knows or cares what drives the transition. Slice 2.2 has no real processing, so
/// the only caller is a Development-only verification endpoint; Slice 2.3's extraction work is
/// expected to call this same handler when processing actually starts, succeeds or fails. That is
/// why the rules live here and not in an endpoint.
/// </remarks>
public sealed class TransitionDocumentProcessingStatusHandler
{
    /// <summary>
    /// Reported when the requested status change is not one the document's current status allows
    /// — for example starting processing on a document that is already being processed.
    /// </summary>
    /// <remarks>
    /// Deliberately worded differently from <see cref="ConcurrentlyModifiedMessage"/>. Both are
    /// 409s, but they mean different things: this one says the request was never permitted, while
    /// the other says it was permitted and lost a race. A caller must be able to tell them apart
    /// from the response body, since only the second is worth retrying.
    ///
    /// It does not name the current or requested status. The transition table is a fixed,
    /// three-entry rule set, so naming them adds nothing a caller can act on, and the message
    /// stays stable for tests.
    /// </remarks>
    public const string InvalidTransitionMessage =
        "This document's processing status does not allow the requested change.";

    /// <summary>
    /// Reported when the status change itself was legal but another writer moved the document
    /// first. Shares the exact text used by the other document writers that lose this race, so
    /// "someone else changed this, reload" reads identically wherever it happens.
    /// </summary>
    public const string ConcurrentlyModifiedMessage =
        UpdateDocumentHandler.ConcurrentlyModifiedMessage;

    /// <summary>
    /// The complete set of permitted moves. Anything absent from it is rejected, including
    /// same-status "moves" and anything out of a null status (a document with no uploaded file
    /// has nothing to process). Retry (Failed → Processing) and reprocess (Ready → Processing)
    /// are deliberately absent in this slice.
    /// </summary>
    private static readonly HashSet<(string From, string To)> LegalTransitions =
    [
        (DocumentProcessingStatuses.Uploaded, DocumentProcessingStatuses.Processing),
        (DocumentProcessingStatuses.Processing, DocumentProcessingStatuses.Ready),
        (DocumentProcessingStatuses.Processing, DocumentProcessingStatuses.Failed)
    ];

    private readonly IDocumentRepository _documentRepository;
    private readonly IProjectMembershipService _membershipService;
    private readonly IValidator<TransitionDocumentProcessingStatusCommand> _validator;

    public TransitionDocumentProcessingStatusHandler(
        IDocumentRepository documentRepository,
        IProjectMembershipService membershipService,
        IValidator<TransitionDocumentProcessingStatusCommand> validator)
    {
        _documentRepository = documentRepository;
        _membershipService = membershipService;
        _validator = validator;
    }

    /// <summary>
    /// Whether a document currently in <paramref name="from"/> (null meaning "no file uploaded
    /// yet") may move to <paramref name="to"/>.
    /// </summary>
    public static bool IsLegalTransition(string? from, string to) =>
        from is not null && LegalTransitions.Contains((from, to));

    public async Task<DocumentMetadata> HandleAsync(
        TransitionDocumentProcessingStatusCommand command,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(command, cancellationToken);

        var document = await _documentRepository.GetByIdAsync(
            command.DocumentId,
            cancellationToken);

        if (document is null)
        {
            // Must stay textually identical to the membership-check 404 below — same
            // indistinguishability rationale as the other document handlers.
            throw NotFoundException.HiddenResource();
        }

        await _membershipService.EnsureMemberAsync(
            document.ProjectId,
            userId,
            cancellationToken);

        if (!IsLegalTransition(document.Status, command.TargetStatus))
        {
            throw new ConflictException(InvalidTransitionMessage);
        }

        // Only the processing status changes. UpdatedAt tracks changes to the document's own
        // metadata, and a lifecycle transition — which Slice 2.3 will raise from processing code
        // rather than from a person — is not an edit of the document.
        document.Status = command.TargetStatus;

        try
        {
            await _documentRepository.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // Two transitions out of the same status can both pass the check above before either
            // commits; the row's concurrency token is what settles that, and the loser's write
            // never reaches the database. Reported as a distinct 409 rather than being allowed to
            // fall through to the generic 500 handler, so the caller can tell "not allowed" from
            // "you lost a race".
            throw new ConflictException(ConcurrentlyModifiedMessage);
        }

        return document;
    }
}
