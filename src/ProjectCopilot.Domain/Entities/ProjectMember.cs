namespace ProjectCopilot.Domain.Entities;

public sealed class ProjectMember
{
    public Guid ProjectId { get; set; }

    public Guid UserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
