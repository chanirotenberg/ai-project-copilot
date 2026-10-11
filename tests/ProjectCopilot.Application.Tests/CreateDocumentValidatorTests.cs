using ProjectCopilot.Application.Documents.CreateDocument;

namespace ProjectCopilot.Application.Tests;

public class CreateDocumentValidatorTests
{
    [Fact]
    public async Task ValidateAsync_WhenTitleIsEmpty_ShouldFail()
    {
        var validator = new CreateDocumentValidator();

        var command = new CreateDocumentCommand(Guid.NewGuid(), "", null);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(CreateDocumentCommand.Title));
    }

    [Fact]
    public async Task ValidateAsync_WhenTitleExceedsMaxLength_ShouldFail()
    {
        var validator = new CreateDocumentValidator();

        var command = new CreateDocumentCommand(Guid.NewGuid(), new string('a', 201), null);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(CreateDocumentCommand.Title));
    }

    [Fact]
    public async Task ValidateAsync_WhenDescriptionExceedsMaxLength_ShouldFail()
    {
        var validator = new CreateDocumentValidator();

        var command = new CreateDocumentCommand(Guid.NewGuid(), "Valid Title", new string('a', 2001));

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(CreateDocumentCommand.Description));
    }

    [Fact]
    public async Task ValidateAsync_WhenProjectIdIsEmpty_ShouldFail()
    {
        var validator = new CreateDocumentValidator();

        var command = new CreateDocumentCommand(Guid.Empty, "Valid Title", null);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(CreateDocumentCommand.ProjectId));
    }

    [Fact]
    public async Task ValidateAsync_WhenCommandIsValid_ShouldPass()
    {
        var validator = new CreateDocumentValidator();

        var command = new CreateDocumentCommand(Guid.NewGuid(), "Valid Title", "Valid description");

        var result = await validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }
}
