using ProjectCopilot.Application.Auth.Register;

namespace ProjectCopilot.Application.Tests;

public class RegisterValidatorTests
{
    [Fact]
    public async Task ValidateAsync_WhenEmailIsEmpty_ShouldFail()
    {
        var validator = new RegisterValidator();

        var command = new RegisterCommand("", "ValidPass123");

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(RegisterCommand.Email));
    }

    [Fact]
    public async Task ValidateAsync_WhenEmailFormatIsInvalid_ShouldFail()
    {
        var validator = new RegisterValidator();

        var command = new RegisterCommand("not-an-email", "ValidPass123");

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(RegisterCommand.Email));
    }

    [Fact]
    public async Task ValidateAsync_WhenPasswordIsEmpty_ShouldFail()
    {
        var validator = new RegisterValidator();

        var command = new RegisterCommand("user@test.local", "");

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(RegisterCommand.Password));
    }

    [Fact]
    public async Task ValidateAsync_WhenPasswordIsTooShort_ShouldFail()
    {
        var validator = new RegisterValidator();

        var command = new RegisterCommand("user@test.local", "short1");

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(RegisterCommand.Password));
    }

    [Fact]
    public async Task ValidateAsync_WhenCommandIsValid_ShouldPass()
    {
        var validator = new RegisterValidator();

        var command = new RegisterCommand("user@test.local", "ValidPass123");

        var result = await validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }
}
