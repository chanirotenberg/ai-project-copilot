using FluentValidation;

namespace ProjectCopilot.Application.Documents.CreateDocument;

public sealed class CreateDocumentValidator : AbstractValidator<CreateDocumentCommand>
{
    public CreateDocumentValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty();

        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(2000);
    }
}
