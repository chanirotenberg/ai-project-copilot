using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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

    // Anti-enumeration contract: a successful registration returns a generic 200 OK with no
    // userId/email echoed back (see AuthEndpoints/IdentityService.RegisterAsync). Proving a
    // real account was actually created is done indirectly, via a subsequent successful login.
    [Fact]
    public async Task Register_WithNewEmail_ShouldReturnGenericAcknowledgmentAndCreateRealAccount()
    {
        using var client = _factory.CreateClient();
        var email = NewEmail();
        const string password = "ValidPass123";

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterCommand(email, password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Registration request accepted.", body.GetProperty("message").GetString());

        // Prove a real account was created: login with the exact submitted credentials works.
        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginCommand(email, password));

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }

    // Anti-enumeration contract: registering the same email twice must return the exact same
    // generic 200 OK response both times — nothing in the response may reveal that the second
    // attempt collided with an existing account. Also proves only one real account exists for
    // that email (the second attempt's password is never applied to any account).
    [Fact]
    public async Task Register_WithDuplicateEmail_ShouldReturnIdenticalGenericAcknowledgment()
    {
        using var client = _factory.CreateClient();
        var email = NewEmail();
        const string originalPassword = "ValidPass123";
        const string secondAttemptPassword = "DifferentPass456";

        var firstResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterCommand(email, originalPassword));

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var firstBody = await firstResponse.Content.ReadAsStringAsync();

        var secondResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterCommand(email, secondAttemptPassword));

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var secondBody = await secondResponse.Content.ReadAsStringAsync();

        // Byte-for-byte identical: same status code, same raw response body string - not just a
        // structurally-equal-ish comparison of one field.
        Assert.Equal(firstResponse.StatusCode, secondResponse.StatusCode);
        Assert.Equal(firstBody, secondBody);

        // Only one real account exists: the original password still works...
        var loginWithOriginalPassword = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginCommand(email, originalPassword));
        Assert.Equal(HttpStatusCode.OK, loginWithOriginalPassword.StatusCode);

        // ...and the second attempt's password was never applied to any account.
        var loginWithSecondAttemptPassword = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginCommand(email, secondAttemptPassword));
        Assert.Equal(HttpStatusCode.Unauthorized, loginWithSecondAttemptPassword.StatusCode);
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

    // Anti-enumeration contract, weak-password branch: UserManager.CreateAsync's
    // UpdatePasswordHash validates password strength BEFORE it ever reaches the email-uniqueness
    // check (see IdentityService.RegisterAsync), so for a given weak password the code path never
    // depends on whether the submitted email already exists - the uniqueness check is never
    // reached at all in that branch. The real security property this proves is not "duplicate
    // email always returns the generic 200" (that only holds on the strong-password branch,
    // asserted separately below) - it is "the response never depends on email existence, for a
    // given password." This test proves that directly by comparing full responses (status + raw
    // body), not just a status code or a single field, for a brand-new email vs. an
    // already-registered email, both using the exact same weak password.
    [Fact]
    public async Task Register_WithWeakPassword_ShouldReturnIdenticalResponseForNewAndDuplicateEmail()
    {
        using var client = _factory.CreateClient();
        const string weakPassword = "short";

        // Brand-new email, weak password.
        var newEmail = NewEmail();
        var newEmailResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterCommand(newEmail, weakPassword));
        var newEmailBody = await newEmailResponse.Content.ReadAsStringAsync();

        // Already-registered email (registered first with a strong password so a real account
        // exists), then a duplicate attempt using the exact same weak password as above.
        var duplicateEmail = NewEmail();
        const string strongPassword = "ValidPass123";
        var firstRegisterResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterCommand(duplicateEmail, strongPassword));
        Assert.Equal(HttpStatusCode.OK, firstRegisterResponse.StatusCode);

        var duplicateEmailResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterCommand(duplicateEmail, weakPassword));
        var duplicateEmailBody = await duplicateEmailResponse.Content.ReadAsStringAsync();

        // Byte-for-byte identical: same status code, same raw response body string.
        Assert.Equal(newEmailResponse.StatusCode, duplicateEmailResponse.StatusCode);
        Assert.Equal(newEmailBody, duplicateEmailBody);
    }

    // Password policy (Program.cs AddIdentityCore): RequiredLength=10, RequireDigit=true,
    // RequireLowercase=true, RequireUppercase=false, RequireNonAlphanumeric=false. These
    // exercise Identity's own character-class enforcement directly - FluentValidation's
    // RegisterValidator only checks length, not composition, so these must be integration-level.

    [Fact]
    public async Task Register_WithPasswordUnderTenChars_ShouldReturnBadRequest()
    {
        using var client = _factory.CreateClient();

        // 9 chars, otherwise valid (lowercase + digit) - isolates the length rule specifically.
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterCommand(NewEmail(), "abcdefg12"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithTenCharsLowercaseAndDigit_ShouldSucceed()
    {
        using var client = _factory.CreateClient();

        // Exactly 10 chars, lowercase + digit, no uppercase, no special character.
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterCommand(NewEmail(), "abcdefgh12"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithoutUppercase_ShouldStillSucceed()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterCommand(NewEmail(), "nouppercase1"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithoutSpecialCharacter_ShouldStillSucceed()
    {
        using var client = _factory.CreateClient();

        // Plain alphanumeric, no special/non-alphanumeric character at all.
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterCommand(NewEmail(), "plainalnum1"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithoutDigit_ShouldReturnBadRequest()
    {
        using var client = _factory.CreateClient();

        // 10+ chars, lowercase only, no digit.
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterCommand(NewEmail(), "nodigitsatall"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithoutLowercase_ShouldReturnBadRequest()
    {
        using var client = _factory.CreateClient();

        // 10+ chars, uppercase + digit, no lowercase at all.
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterCommand(NewEmail(), "NOLOWERCASE1"));

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

        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

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

        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

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

        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

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
