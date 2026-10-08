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

        // Calendar-day comparison (not instant), matching Project.Deadline's approved
        // semantics: a date-only "today" due date (as a native <input type="date"> sends)
        // is allowed; only a genuinely past date is rejected.
        RuleFor(x => x.DueDate)
            .Must(dueDate => dueDate is null || dueDate.Value.Date >= DateTime.UtcNow.Date)
            .WithMessage("Due date must not be before today.");
    }
}