using FluentValidation;

namespace ProjectCopilot.Application.Tasks.CreateTask;

public sealed class CreateTaskValidator : AbstractValidator<CreateTaskCommand>
{
    private static readonly string[] AllowedPriorities =
    [
        "Low",
        "Medium",
        "High",
        "Critical"
    ];

    public CreateTaskValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty();

        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(2000);

        RuleFor(x => x.Priority)
            .NotEmpty()
            .Must(priority => AllowedPriorities.Contains(priority))
            .WithMessage("Priority must be one of: Low, Medium, High, Critical.");

        RuleFor(x => x.DueDate)
            .Must(dueDate => dueDate is null || dueDate.Value > DateTime.UtcNow)
            .WithMessage("Due date must be in the future.");
    }
}