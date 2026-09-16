using FluentValidation;

namespace ProjectCopilot.Application.Tasks.UpdateTask;

public sealed class UpdateTaskValidator : AbstractValidator<UpdateTaskCommand>
{
    private static readonly string[] AllowedStatuses =
    [
        "Todo",
        "InProgress",
        "Done"
    ];

    private static readonly string[] AllowedPriorities =
    [
        "Low",
        "Medium",
        "High",
        "Critical"
    ];

    public UpdateTaskValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(2000);

        RuleFor(x => x.Status)
            .NotEmpty()
            .Must(status => AllowedStatuses.Contains(status))
            .WithMessage("Status must be one of: Todo, InProgress, Done.");

        RuleFor(x => x.Priority)
            .NotEmpty()
            .Must(priority => AllowedPriorities.Contains(priority))
            .WithMessage("Priority must be one of: Low, Medium, High, Critical.");
    }
}