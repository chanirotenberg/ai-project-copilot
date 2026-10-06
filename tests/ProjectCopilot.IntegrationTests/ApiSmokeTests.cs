using System.Net;
using System.Net.Http.Headers;

namespace ProjectCopilot.IntegrationTests;

public class ApiSmokeTests : IClassFixture<ProjectCopilotWebApplicationFactory>
{
    private readonly ProjectCopilotWebApplicationFactory _factory;

    public ApiSmokeTests(ProjectCopilotWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetProjects_WithValidToken_ShouldReturnSuccessStatusCode()
    {
        using var client = _factory.CreateClient();

        var (accessToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(client);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetAsync("/api/v1/projects");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetProjects_WithoutToken_ShouldReturnUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/projects");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
