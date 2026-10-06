using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ProjectCopilot.Application.Projects.CreateProject;
using ProjectCopilot.Domain.Entities;

namespace ProjectCopilot.IntegrationTests;

public class ProjectsAuthorizationTests : IClassFixture<ProjectCopilotWebApplicationFactory>
{
    private readonly ProjectCopilotWebApplicationFactory _factory;

    public ProjectsAuthorizationTests(ProjectCopilotWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetProjectById_WithoutToken_ShouldReturnUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/projects/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetProjectById_WithTamperedToken_ShouldReturnUnauthorized()
    {
        using var client = _factory.CreateClient();

        const string tamperedKey = "a-completely-different-signing-key-32bytes-min";

        var handler = new JsonWebTokenHandler();
        var tamperedToken = handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = ProjectCopilotWebApplicationFactory.TestJwtIssuer,
            Audience = ProjectCopilotWebApplicationFactory.TestJwtAudience,
            Expires = DateTime.UtcNow.AddMinutes(60),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tamperedKey)),
                SecurityAlgorithms.HmacSha256)
        });

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tamperedToken);

        var response = await client.GetAsync($"/api/v1/projects/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetProjectById_WithValidToken_ShouldReturnProject()
    {
        using var client = _factory.CreateClient();

        var (accessToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(client);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/projects",
            new CreateProjectCommand("Auth Test Project", null, null));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createdProject = await createResponse.Content.ReadFromJsonAsync<Project>();

        Assert.NotNull(createdProject);

        var getResponse = await client.GetAsync($"/api/v1/projects/{createdProject!.Id}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var fetchedProject = await getResponse.Content.ReadFromJsonAsync<Project>();

        Assert.NotNull(fetchedProject);
        Assert.Equal(createdProject.Id, fetchedProject!.Id);
    }
}
