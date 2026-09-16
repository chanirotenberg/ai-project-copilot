namespace ProjectCopilot.Application.Tasks.CreateTask;

public sealed record CreateTaskCommand(
    Guid ProjectId,
    string Title,
    string? Description,
    string Priority,
    Guid? AssignedUserId,
    DateTime? DueDate);