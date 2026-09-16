namespace ProjectCopilot.Application.Tasks.UpdateTask;

public sealed record UpdateTaskCommand(
    Guid Id,
    string Title,
    string? Description,
    string Status,
    string Priority,
    Guid? AssignedUserId,
    DateTime? DueDate,
    bool IsBlocked);