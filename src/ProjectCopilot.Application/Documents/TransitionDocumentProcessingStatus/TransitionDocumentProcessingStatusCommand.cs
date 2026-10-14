namespace ProjectCopilot.Application.Documents.TransitionDocumentProcessingStatus;

/// <summary>
/// Moves one document to another processing status.
/// </summary>
/// <param name="DocumentId">
/// The target document. The owning project is resolved server-side from the stored document, so
/// the command deliberately carries no project id for a caller to point elsewhere.
/// </param>
/// <param name="TargetStatus">
/// The status to move to — one of <see cref="DocumentProcessingStatuses"/>. Whether the move is
/// permitted depends on the document's current status and is decided by
/// <see cref="TransitionDocumentProcessingStatusHandler"/>, not here.
/// </param>
public sealed record TransitionDocumentProcessingStatusCommand(
    Guid DocumentId,
    string TargetStatus);
