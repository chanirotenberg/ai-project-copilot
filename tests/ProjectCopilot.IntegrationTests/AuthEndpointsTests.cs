using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ProjectCopilot.Application.Auth.Login;
using ProjectCopilot.Application.Auth.Register;

namespace ProjectCopilot.IntegrationTests;

public class AuthEndpointsTests : IClassFixture<ProjectCopilotWebApplicationFactory>
{
    private readonly ProjectCopilotWebApplicationFactory _factory;

    public AuthEndpointsTests(ProjectCopilotWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@test.local";

    [Fact]
    public async Task Register_WithNewEmail_ShouldReturnCreated()
    {
        using var client = _factory.CreateClient();
        var email = NewEmail();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterCommand(email, "ValidPass123"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<RegisterResult>();

        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result!.UserId);
        Assert.Equal(email, result.Email);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ShouldReturnBadRequest()
    {
        using var client = _factory.CreateClient();
        var email = NewEmail();

        var firstResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterCommand(email, "ValidPass123"));

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        var secondResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterCommand(email, "ValidPass123"));

        Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);
    }

    [Fact]
    public async Task Register_WithWeakPassword_ShouldReturnBadRequest()
    {
        using var client = _factory.CreateClient();
        var email = NewEmail();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterCommand(email, "short"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithCorrectCredentials_ShouldReturnValidToken()
    {
        using var client = _factory.CreateClient();
        var email = NewEmail();
        const string password = "ValidPass123";

        var registerResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterCommand(email, password));

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginCommand(email, password));

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var result = await loginResponse.Content.ReadFromJsonAsync<LoginResult>();

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result!.AccessToken));

        var validationParameters = new TokenValidationParameters
        {
            ValidIssuer = ProjectCopilotWebApplicationFactory.TestJwtIssuer,
            ValidAudience = ProjectCopilotWebApplicationFactory.TestJwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(ProjectCopilotWebApplicationFactory.TestJwtKey)),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true
        };

        var handler = new JsonWebTokenHandler();
        var validationResult = await handler.ValidateTokenAsync(result.AccessToken, validationParameters);

        Assert.True(validationResult.IsValid, validationResult.Exception?.ToString());

        var subClaim = validationResult.ClaimsIdentity.FindFirst("sub");
        var emailClaim = validationResult.ClaimsIdentity.FindFirst("email");

        Assert.NotNull(subClaim);
        Assert.Equal(result.UserId.ToString(), subClaim!.Value);

        Assert.NotNull(emailClaim);
        Assert.Equal(result.Email, emailClaim!.Value);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ShouldReturnUnauthorized()
    {
        using var client = _factory.CreateClient();
        var email = NewEmail();

        var registerResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterCommand(email, "ValidPass123"));

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginCommand(email, "WrongPass123"));

        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
    }

    [Fact]
    public async Task Login_WithNonExistentEmail_ShouldReturnUnauthorized()
    {
        using var client = _factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginCommand(NewEmail(), "SomePass123"));

        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
    }

    // Guards against re-introducing a message-level leak between "no such user" and
    // "wrong password" (the timing side-channel itself is not asserted here - see
    // IdentityService.ValidateCredentialsAsync for the dummy-hash mitigation).
    [Fact]
    public async Task Login_WithWrongPasswordAndWithNonExistentEmail_ShouldReturnSameUnauthorizedResponse()
    {
        using var client = _factory.CreateClient();
        var email = NewEmail();

        var registerResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterCommand(email, "ValidPass123"));

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var wrongPasswordResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginCommand(email, "WrongPass123"));

        var nonExistentEmailResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginCommand(NewEmail(), "SomePass123"));

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPasswordResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, nonExistentEmailResponse.StatusCode);

        var wrongPasswordProblem = await wrongPasswordResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        var nonExistentEmailProblem = await nonExistentEmailResponse.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(wrongPasswordProblem);
        Assert.NotNull(nonExistentEmailProblem);
        Assert.Equal(wrongPasswordProblem!.Detail, nonExistentEmailProblem!.Detail);
        Assert.Equal(wrongPasswordProblem.Title, nonExistentEmailProblem.Title);
    }
}
