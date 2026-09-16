using ProjectCopilot.Domain.Entities;

namespace ProjectCopilot.Domain.Tests;

public class TaskItemTests
{
    [Fact]
    public void NewTaskItem_CanBeCreatedWithExpectedValues()
    {
        var projectId = Guid.NewGuid();

        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = "Implement retry logic",
            Description = "Handle failed payment retries",
            Status = "Todo",
            Priority = "High",
            IsBlocked = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        Assert.Equal(projectId, task.ProjectId);
        Assert.Equal("Implement retry logic", task.Title);
        Assert.Equal("Todo", task.Status);
        Assert.Equal("High", task.Priority);
        Assert.False(task.IsBlocked);
    }
}