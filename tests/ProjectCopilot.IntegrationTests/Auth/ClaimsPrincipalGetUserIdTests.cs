using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ProjectCopilot.Application.Projects.CreateProject;
using ProjectCopilot.Domain.Entities;

namespace ProjectCopilot.IntegrationTests;

// Verifies a load-bearing assumption before Project-membership authorization code builds on
// it at multiple call sites: that ClaimsPrincipal.GetUserId() (ProjectCopilot.Api.Authorization)
// correctly recovers the registered user's id from the REAL ASP.NET Core JWT authentication
// pipeline (JwtTokenGenerator's issued "sub" claim, through real token validation) rather than
// from a hand-built ClaimsPrincipal. The POST /api/v1/projects endpoint is the vehicle: its
// handler sets Project.CreatedByUserId from user.GetUserId(), so asserting the created
// project's CreatedByUserId equals the id returned by login proves the claim is read correctly
// end-to-end.
public class ClaimsPrincipalGetUserIdTests : IClassFixture<ProjectCopilotWebApplicationFactory>
{
    private readonly ProjectCopilotWebApplicationFactory _factory;

    public ClaimsPrincipalGetUserIdTests(ProjectCopilotWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetUserId_ThroughRealJwtPipeline_ShouldReturnRegisteredUserId()
    {
        using var client = _factory.CreateClient();

        var (accessToken, _, userId, _) = await AuthTestHelper.RegisterAndLoginAsync(client);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PostAsJsonAsync(
            "/api/v1/projects",
            new CreateProjectCommand("GetUserId Verification Project", null, null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var project = await response.Content.ReadFromJsonAsync<Project>();

        Assert.NotNull(project);
        Assert.Equal(userId, project!.CreatedByUserId);
    }
}
