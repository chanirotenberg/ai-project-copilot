using ProjectCopilot.Application.Tasks.CreateTask;

namespace ProjectCopilot.Application.Tests;

public class CreateTaskValidatorTests
{
    [Fact]
    public async Task ValidateAsync_WhenTitleIsEmpty_ShouldFail()
    {
        var validator = new CreateTaskValidator();

        var command = new CreateTaskCommand(
            Guid.NewGuid(),
            "",
            "Test description",
            "High",
            null,
            DateTime.UtcNow.AddDays(5));

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(CreateTaskCommand.Title));
    }

    [Fact]
    public async Task ValidateAsync_WhenPriorityIsInvalid_ShouldFail()
    {
        var validator = new CreateTaskValidator();

        var command = new CreateTaskCommand(
            Guid.NewGuid(),
            "Valid title",
            "Test description",
            "SuperHigh",
            null,
            DateTime.UtcNow.AddDays(5));

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(CreateTaskCommand.Priority));
    }

    [Fact]
    public async Task ValidateAsync_WhenCommandIsValid_ShouldPass()
    {
        var validator = new CreateTaskValidator();

        var command = new CreateTaskCommand(
            Guid.NewGuid(),
            "Implement retry logic",
            "Handle failed payment retries",
            "High",
            null,
            DateTime.UtcNow.AddDays(5));

        var result = await validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }
}