using ProjectCopilot.Application.Documents.UpdateDocument;

namespace ProjectCopilot.Application.Tests;

public class UpdateDocumentValidatorTests
{
    [Fact]
    public async Task ValidateAsync_WhenTitleIsEmpty_ShouldFail()
    {
        var validator = new UpdateDocumentValidator();

        var command = new UpdateDocumentCommand(Guid.NewGuid(), "", null);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(UpdateDocumentCommand.Title));
    }

    [Fact]
    public async Task ValidateAsync_WhenTitleExceedsMaxLength_ShouldFail()
    {
        var validator = new UpdateDocumentValidator();

        var command = new UpdateDocumentCommand(Guid.NewGuid(), new string('a', 201), null);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(UpdateDocumentCommand.Title));
    }

    [Fact]
    public async Task ValidateAsync_WhenDescriptionExceedsMaxLength_ShouldFail()
    {
        var validator = new UpdateDocumentValidator();

        var command = new UpdateDocumentCommand(Guid.NewGuid(), "Valid Title", new string('a', 2001));

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(UpdateDocumentCommand.Description));
    }

    [Fact]
    public async Task ValidateAsync_WhenIdIsEmpty_ShouldFail()
    {
        var validator = new UpdateDocumentValidator();

        var command = new UpdateDocumentCommand(Guid.Empty, "Valid Title", null);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(UpdateDocumentCommand.Id));
    }

    [Fact]
    public async Task ValidateAsync_WhenCommandIsValid_ShouldPass()
    {
        var validator = new UpdateDocumentValidator();

        var command = new UpdateDocumentCommand(Guid.NewGuid(), "Valid Title", "Valid description");

        var result = await validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }
}
