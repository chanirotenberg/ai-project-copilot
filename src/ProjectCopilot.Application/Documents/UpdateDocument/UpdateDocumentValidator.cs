using FluentValidation;

namespace ProjectCopilot.Application.Documents.UpdateDocument;

public sealed class UpdateDocumentValidator : AbstractValidator<UpdateDocumentCommand>
{
    public UpdateDocumentValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(2000);
    }
}
