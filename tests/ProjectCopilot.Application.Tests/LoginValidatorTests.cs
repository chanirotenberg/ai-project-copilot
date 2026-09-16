using ProjectCopilot.Application.Auth.Login;

namespace ProjectCopilot.Application.Tests;

public class LoginValidatorTests
{
    [Fact]
    public async Task ValidateAsync_WhenEmailIsEmpty_ShouldFail()
    {
        var validator = new LoginValidator();

        var command = new LoginCommand("", "SomePass123");

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(LoginCommand.Email));
    }

    [Fact]
    public async Task ValidateAsync_WhenPasswordIsEmpty_ShouldFail()
    {
        var validator = new LoginValidator();

        var command = new LoginCommand("user@test.local", "");

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(LoginCommand.Password));
    }

    [Fact]
    public async Task ValidateAsync_WhenCommandIsValid_ShouldPass()
    {
        var validator = new LoginValidator();

        var command = new LoginCommand("user@test.local", "SomePass123");

        var result = await validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }
}
