using FluentValidation;

namespace ProjectCopilot.Application.Documents.TransitionDocumentProcessingStatus;

/// <summary>
/// Shape validation only: the requested status must be a known one.
/// </summary>
/// <remarks>
/// Whether the move from the document's <em>current</em> status to this one is permitted is a
/// business rule, not input validation — it needs the stored document, so it lives in
/// <see cref="TransitionDocumentProcessingStatusHandler"/> and is reported as a 409 rather than a
/// 400. An unrecognised status string, by contrast, is a malformed request.
/// </remarks>
public sealed class TransitionDocumentProcessingStatusValidator
    : AbstractValidator<TransitionDocumentProcessingStatusCommand>
{
    public TransitionDocumentProcessingStatusValidator()
    {
        RuleFor(x => x.DocumentId)
            .NotEmpty();

        RuleFor(x => x.TargetStatus)
            .NotEmpty()
            .Must(DocumentProcessingStatuses.IsKnown)
            .WithMessage(
                $"Status must be one of: {string.Join(", ", DocumentProcessingStatuses.All)}.");
    }
}
