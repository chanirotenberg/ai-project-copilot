using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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
    public async Task GetProjects_WithTamperedToken_ShouldReturnUnauthorized()
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

        var response = await client.GetAsync("/api/v1/projects");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateProject_WithoutToken_ShouldReturnUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/projects",
            new CreateProjectCommand("Unauthorized Create Project", null, null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateProject_WithTamperedToken_ShouldReturnUnauthorized()
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

        var response = await client.PostAsJsonAsync(
            "/api/v1/projects",
            new CreateProjectCommand("Unauthorized Create Project", null, null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetProjectById_WithExpiredToken_ShouldReturnUnauthorized()
    {
        using var client = _factory.CreateClient();

        // Correctly signed with the REAL test signing key/issuer/audience, but already expired.
        // Isolates "lifetime validation" (ValidateLifetime=true in Program.cs) from "signature
        // validation" (covered separately by the tampered-token tests above).
        var handler = new JsonWebTokenHandler();
        var expiredToken = handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = ProjectCopilotWebApplicationFactory.TestJwtIssuer,
            Audience = ProjectCopilotWebApplicationFactory.TestJwtAudience,
            IssuedAt = DateTime.UtcNow.AddMinutes(-20),
            NotBefore = DateTime.UtcNow.AddMinutes(-20),
            Expires = DateTime.UtcNow.AddMinutes(-10),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ProjectCopilotWebApplicationFactory.TestJwtKey)),
                SecurityAlgorithms.HmacSha256)
        });

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", expiredToken);

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

    [Fact]
    public async Task CreateProject_WithSpoofedCreatedByUserIdInBody_ShouldIgnoreItAndUseAuthenticatedUser()
    {
        using var client = _factory.CreateClient();

        var (accessToken, _, userId, _) = await AuthTestHelper.RegisterAndLoginAsync(client);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        // Deliberately different from the real authenticated user, to prove it cannot
        // override server-derived identity even when smuggled in via an unexpected
        // extra JSON property that CreateProjectCommand does not declare.
        var spoofedUserId = Guid.NewGuid();

        var payload = JsonSerializer.Serialize(new
        {
            Name = "Spoof Test Project",
            Description = (string?)null,
            Deadline = (DateTime?)null,
            CreatedByUserId = spoofedUserId
        });

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/v1/projects", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var createdProject = await response.Content.ReadFromJsonAsync<Project>();

        Assert.NotNull(createdProject);
        Assert.Equal(userId, createdProject!.CreatedByUserId);
        Assert.NotEqual(spoofedUserId, createdProject.CreatedByUserId);
    }

    [Fact]
    public async Task CreateProject_WithDateOnlyDeadline_ShouldReturnCreatedNotInternalServerError()
    {
        using var client = _factory.CreateClient();

        var (accessToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(client);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        // Raw JSON, deliberately NOT built from CreateProjectCommand, so that the "deadline"
        // string is deserialized by System.Text.Json exactly the way a native HTML
        // <input type="date"> sends it: no time component, no timezone offset. That produces
        // a DateTime with Kind = Unspecified, which previously made Npgsql throw when writing
        // to the "timestamp with time zone" Deadline column, surfacing as a 500.
        const string payload = """
            {"name":"Date-Only Deadline Project","description":null,"deadline":"2026-11-07"}
            """;

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/v1/projects", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var createdProject = await response.Content.ReadFromJsonAsync<Project>();

        Assert.NotNull(createdProject);
        Assert.NotNull(createdProject!.Deadline);
        Assert.Equal(new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc), createdProject.Deadline!.Value.ToUniversalTime());
    }

    [Fact]
    public async Task CreateProject_WithExplicitUtcDeadline_ShouldPersistItUnchanged()
    {
        using var client = _factory.CreateClient();

        var (accessToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(client);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var utcDeadline = new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc);

        var response = await client.PostAsJsonAsync(
            "/api/v1/projects",
            new CreateProjectCommand("Explicit UTC Deadline Project", null, utcDeadline));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var createdProject = await response.Content.ReadFromJsonAsync<Project>();

        Assert.NotNull(createdProject);
        Assert.NotNull(createdProject!.Deadline);
        Assert.Equal(utcDeadline, createdProject.Deadline!.Value.ToUniversalTime());
    }

    [Fact]
    public async Task CreateProject_WithLocalKindDeadline_ShouldConvertToUtcInstantNotRelabel()
    {
        using var client = _factory.CreateClient();

        var (accessToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(client);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        // Constructed with Kind = Local so the handler's NormalizeToUtc must call
        // .ToUniversalTime() (shifting the instant) rather than merely relabeling it as UTC.
        var localDeadline = new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Local);
        var expectedUtcInstant = localDeadline.ToUniversalTime();

        var response = await client.PostAsJsonAsync(
            "/api/v1/projects",
            new CreateProjectCommand("Local Kind Deadline Project", null, localDeadline));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var createdProject = await response.Content.ReadFromJsonAsync<Project>();

        Assert.NotNull(createdProject);
        Assert.NotNull(createdProject!.Deadline);
        Assert.Equal(expectedUtcInstant, createdProject.Deadline!.Value.ToUniversalTime());
    }
}
