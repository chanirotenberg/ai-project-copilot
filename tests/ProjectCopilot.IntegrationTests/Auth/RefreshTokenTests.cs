using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectCopilot.Application.Abstractions;
using ProjectCopilot.Application.Auth.Login;
using ProjectCopilot.Application.Auth.Refresh;
using ProjectCopilot.Application.Common.Exceptions;
using ProjectCopilot.Infrastructure.Persistence;

namespace ProjectCopilot.IntegrationTests;

public class RefreshTokenTests : IClassFixture<ProjectCopilotWebApplicationFactory>
{
    private readonly ProjectCopilotWebApplicationFactory _factory;

    public RefreshTokenTests(ProjectCopilotWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturnAccessTokenAndRefreshToken()
    {
        using var client = _factory.CreateClient();

        var (accessToken, refreshToken, _, _) = await AuthTestHelper.RegisterAndLoginAsync(client);

        Assert.False(string.IsNullOrWhiteSpace(accessToken));
        Assert.False(string.IsNullOrWhiteSpace(refreshToken));
        Assert.NotEqual(accessToken, refreshToken);
    }

    [Fact]
    public async Task Refresh_WithValidToken_ShouldReturnNewAccessTokenAndNewRefreshToken()
    {
        using var client = _factory.CreateClient();
        var (accessToken, refreshToken, userId, email) = await AuthTestHelper.RegisterAndLoginAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshCommand(refreshToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<LoginResult>();

        Assert.NotNull(result);
        Assert.Equal(userId, result!.UserId);
        Assert.Equal(email, result.Email);
        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));
        Assert.NotEqual(accessToken, result.AccessToken);
        Assert.NotEqual(refreshToken, result.RefreshToken);
    }

    [Fact]
    public async Task Refresh_WithRotatedOldToken_ShouldBeRejected()
    {
        using var client = _factory.CreateClient();
        var (_, refreshToken, _, _) = await AuthTestHelper.RegisterAndLoginAsync(client);

        var firstRefresh = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshCommand(refreshToken));

        Assert.Equal(HttpStatusCode.OK, firstRefresh.StatusCode);

        // Reusing the now-rotated old token must be rejected.
        var secondRefresh = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshCommand(refreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, secondRefresh.StatusCode);
    }

    [Fact]
    public async Task Refresh_ReuseOfRotatedToken_ShouldRevokeAllUserRefreshTokens()
    {
        using var client = _factory.CreateClient();
        var (_, refreshToken, _, _) = await AuthTestHelper.RegisterAndLoginAsync(client);

        var firstRefresh = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshCommand(refreshToken));

        Assert.Equal(HttpStatusCode.OK, firstRefresh.StatusCode);

        var firstResult = await firstRefresh.Content.ReadFromJsonAsync<LoginResult>();
        Assert.NotNull(firstResult);

        // Reuse of the old (rotated) token - this is the "theft" signal.
        var reuseResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshCommand(refreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);

        // The *new* token issued by the first (legitimate) rotation must now also be rejected,
        // because reuse-detection revokes the entire token family for this user.
        var refreshWithNewToken = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshCommand(firstResult!.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, refreshWithNewToken.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithExpiredToken_ShouldReturnUnauthorized()
    {
        using var client = _factory.CreateClient();
        var (_, refreshToken, userId, _) = await AuthTestHelper.RegisterAndLoginAsync(client);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var token = await dbContext.RefreshTokens
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAtUtc)
                .FirstAsync();

            token.ExpiresAtUtc = DateTime.UtcNow.AddDays(-1);

            await dbContext.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshCommand(refreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithMissingOrEmptyToken_ShouldReturnBadRequest()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshCommand(""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithUnknownToken_ShouldReturnUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshCommand("a-completely-unknown-refresh-token-value"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SaveChangesAsync_WithConcurrentRotationOfSameToken_ShouldSurfaceAsInvalidCredentials()
    {
        using var client = _factory.CreateClient();
        var (_, refreshToken, _, _) = await AuthTestHelper.RegisterAndLoginAsync(client);

        // Two independent scopes -> two independent AppDbContext instances, each loading its
        // own tracked copy of the same row (same original RowVersion/xmin). This is the
        // standard way to deterministically force a DbUpdateConcurrencyException in an EF
        // test without relying on a real thread race.
        using var scopeA = _factory.Services.CreateScope();
        using var scopeB = _factory.Services.CreateScope();

        var tokenGenerator = scopeA.ServiceProvider.GetRequiredService<IRefreshTokenGenerator>();
        var tokenHash = tokenGenerator.Hash(refreshToken);

        var repositoryA = scopeA.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();
        var repositoryB = scopeB.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();

        var recordA = await repositoryA.FindByHashAsync(tokenHash);
        var recordB = await repositoryB.FindByHashAsync(tokenHash);

        Assert.NotNull(recordA);
        Assert.NotNull(recordB);

        // First rotation "wins": revoke + save via repositoryA commits and advances the row's
        // xmin in the database.
        await repositoryA.RevokeAsync(recordA!.Id, Guid.NewGuid());
        await repositoryA.SaveChangesAsync();

        // repositoryB loaded the row before repositoryA's commit, so its tracked xmin is now
        // stale. Its own SaveChangesAsync must translate the resulting DbUpdateConcurrencyException
        // into InvalidCredentialsException (401 behavior), not let it surface as a raw/500 failure.
        await repositoryB.RevokeAsync(recordB!.Id, Guid.NewGuid());

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => repositoryB.SaveChangesAsync());
    }
}
