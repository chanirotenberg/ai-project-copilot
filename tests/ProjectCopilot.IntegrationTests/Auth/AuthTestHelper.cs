using System.Net;
using System.Net.Http.Json;
using ProjectCopilot.Application.Auth.Login;
using ProjectCopilot.Application.Auth.Register;

namespace ProjectCopilot.IntegrationTests;

// Shared register+login setup for tests that need an authenticated user but are not
// themselves testing register/login behavior (that behavior is covered directly by
// AuthEndpointsTests). Kept in the IntegrationTests namespace (not a nested Auth namespace)
// to match this project's existing flat-namespace convention across test files.
public static class AuthTestHelper
{
    public static string NewEmail() => $"user-{Guid.NewGuid():N}@test.local";

    public static async Task<(string AccessToken, string RefreshToken, Guid UserId, string Email)> RegisterAndLoginAsync(
        HttpClient client,
        string? email = null,
        string password = "ValidPass123")
    {
        email ??= NewEmail();

        var registerResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterCommand(email, password));

        if (registerResponse.StatusCode != HttpStatusCode.Created)
        {
            throw new InvalidOperationException(
                $"AuthTestHelper setup failed: register returned {registerResponse.StatusCode}.");
        }

        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginCommand(email, password));

        if (loginResponse.StatusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException(
                $"AuthTestHelper setup failed: login returned {loginResponse.StatusCode}.");
        }

        var result = await loginResponse.Content.ReadFromJsonAsync<LoginResult>();

        if (result is null)
        {
            throw new InvalidOperationException("AuthTestHelper setup failed: login returned no body.");
        }

        return (result.AccessToken, result.RefreshToken, result.UserId, result.Email);
    }
}
