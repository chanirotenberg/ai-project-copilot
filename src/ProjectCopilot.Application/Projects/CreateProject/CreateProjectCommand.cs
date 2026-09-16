namespace ProjectCopilot.Application.Projects.CreateProject;

public sealed record CreateProjectCommand(
    string Name,
    string? Description,
    DateTime? Deadline,
    Guid CreatedByUserId);