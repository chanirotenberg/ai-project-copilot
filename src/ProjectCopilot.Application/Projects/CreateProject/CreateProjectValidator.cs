using FluentValidation;
using ProjectCopilot.Application.Common;

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

        // Normalized to UTC before comparing, since a raw Kind = Local deadline's .Date would
        // otherwise be implicitly reinterpreted relative to the host's local timezone.
        RuleFor(x => x.Deadline)
            .Must(deadline => deadline is null
                || DateTimeNormalization.NormalizeToUtc(deadline)!.Value.Date >= DateTime.UtcNow.Date)
            .WithMessage("Deadline must not be before today.");
    }
}