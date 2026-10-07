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

        // Known limitation (comment only, not fixed here - out of scope): this compares
        // deadline.Value.Date against DateTime.UtcNow.Date without normalizing DateTimeKind
        // first, so a Kind=Local deadline would compare its local calendar date against the
        // server's UTC calendar date. Not expected to matter in practice - this app's actual
        // frontend only ever sends Kind=Unspecified date-only strings or explicit UTC.
        RuleFor(x => x.Deadline)
            .Must(deadline => deadline is null || deadline.Value.Date >= DateTime.UtcNow.Date)
            .WithMessage("Deadline must not be before today.");
    }
}