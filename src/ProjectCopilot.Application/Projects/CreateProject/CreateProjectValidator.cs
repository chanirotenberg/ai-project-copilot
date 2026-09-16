using FluentValidation;

namespace ProjectCopilot.Application.Projects.CreateProject;

public sealed class CreateProjectValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(2000);

        RuleFor(x => x.Deadline)
            .Must(deadline => deadline is null || deadline.Value > DateTime.UtcNow)
            .WithMessage("Deadline must be in the future.");
    }
}