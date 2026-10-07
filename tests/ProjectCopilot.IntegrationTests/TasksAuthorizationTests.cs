using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ProjectCopilot.Api.Endpoints;
using ProjectCopilot.Application.Projects.CreateProject;
using ProjectCopilot.Application.Tasks.CreateTask;
using ProjectCopilot.Domain.Entities;

namespace ProjectCopilot.IntegrationTests;

// NOTE: the DueDate-normalization tests below (CreateTask_With*DueDate*/UpdateTask_With*DueDate*)
// deliberately mirror the equivalent Project.Deadline tests in ProjectsAuthorizationTests.cs —
// same NormalizeToUtc fix, same three Kind cases (Unspecified/date-only, explicit Utc, Local),
// now proven for both the Create and Update Task handlers.

public class TasksAuthorizationTests : IClassFixture<ProjectCopilotWebApplicationFactory>
{
    private readonly ProjectCopilotWebApplicationFactory _factory;

    public TasksAuthorizationTests(ProjectCopilotWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static CreateTaskCommand PlausibleCreateCommand() =>
        new(
            Guid.NewGuid(),
            "Unauthorized Create Task",
            null,
            "Medium",
            null,
            null);

    private static UpdateTaskRequest PlausibleUpdateRequest() =>
        new(
            "Unauthorized Update Task",
            null,
            "Todo",
            "Medium",
            null,
            null,
            false);

    private static string CreateTamperedToken()
    {
        const string tamperedKey = "a-completely-different-signing-key-32bytes-min";

        var handler = new JsonWebTokenHandler();
        return handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = ProjectCopilotWebApplicationFactory.TestJwtIssuer,
            Audience = ProjectCopilotWebApplicationFactory.TestJwtAudience,
            Expires = DateTime.UtcNow.AddMinutes(60),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tamperedKey)),
                SecurityAlgorithms.HmacSha256)
        });
    }

    [Fact]
    public async Task GetTasks_WithoutToken_ShouldReturnUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/tasks?projectId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetTaskById_WithoutToken_ShouldReturnUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/tasks/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateTask_WithoutToken_ShouldReturnUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/tasks",
            PlausibleCreateCommand());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTask_WithoutToken_ShouldReturnUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/v1/tasks/{Guid.NewGuid()}",
            PlausibleUpdateRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetTasks_WithTamperedToken_ShouldReturnUnauthorized()
    {
        using var client = _factory.CreateClient();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateTamperedToken());

        var response = await client.GetAsync($"/api/v1/tasks?projectId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetTaskById_WithTamperedToken_ShouldReturnUnauthorized()
    {
        using var client = _factory.CreateClient();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateTamperedToken());

        var response = await client.GetAsync($"/api/v1/tasks/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateTask_WithTamperedToken_ShouldReturnUnauthorized()
    {
        using var client = _factory.CreateClient();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateTamperedToken());

        var response = await client.PostAsJsonAsync(
            "/api/v1/tasks",
            PlausibleCreateCommand());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTask_WithTamperedToken_ShouldReturnUnauthorized()
    {
        using var client = _factory.CreateClient();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateTamperedToken());

        var response = await client.PutAsJsonAsync(
            $"/api/v1/tasks/{Guid.NewGuid()}",
            PlausibleUpdateRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetTaskById_WithExpiredToken_ShouldReturnUnauthorized()
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

        var response = await client.GetAsync($"/api/v1/tasks/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Registers+logs in, authorizes <paramref name="client"/>, and creates a project to hang tasks off of.</summary>
    private static async Task<Guid> RegisterLoginAndCreateProjectAsync(HttpClient client, string projectName)
    {
        var (accessToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(client);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var projectResponse = await client.PostAsJsonAsync(
            "/api/v1/projects",
            new CreateProjectCommand(projectName, null, null));
        projectResponse.EnsureSuccessStatusCode();

        var project = await projectResponse.Content.ReadFromJsonAsync<Project>();
        return project!.Id;
    }

    /// <summary>Creates a task with no DueDate (caller's test then exercises Update's DueDate handling).</summary>
    private static async Task<Guid> CreateTaskAsync(HttpClient client, Guid projectId, string title)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/tasks",
            new CreateTaskCommand(projectId, title, null, "Medium", null, null));
        response.EnsureSuccessStatusCode();

        var task = await response.Content.ReadFromJsonAsync<TaskItem>();
        return task!.Id;
    }

    [Fact]
    public async Task CreateTask_WithDateOnlyDueDate_ShouldReturnCreatedNotInternalServerError()
    {
        using var client = _factory.CreateClient();

        var projectId = await RegisterLoginAndCreateProjectAsync(client, "Date-Only DueDate Project");

        // Raw JSON, deliberately NOT built from CreateTaskCommand, so "dueDate" is deserialized
        // by System.Text.Json exactly as a native HTML <input type="date"> sends it: no time,
        // no timezone offset -> Kind = Unspecified, which previously made Npgsql throw when
        // writing to the "timestamp with time zone" DueDate column, surfacing as a 500.
        var payload = $$"""
            {"projectId":"{{projectId}}","title":"Date-Only DueDate Task","description":null,"priority":"Medium","assignedUserId":null,"dueDate":"2026-11-07"}
            """;

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/v1/tasks", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var createdTask = await response.Content.ReadFromJsonAsync<TaskItem>();

        Assert.NotNull(createdTask);
        Assert.NotNull(createdTask!.DueDate);
        Assert.Equal(new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc), createdTask.DueDate!.Value.ToUniversalTime());
    }

    [Fact]
    public async Task CreateTask_WithExplicitUtcDueDate_ShouldPersistItUnchanged()
    {
        using var client = _factory.CreateClient();

        var projectId = await RegisterLoginAndCreateProjectAsync(client, "Explicit UTC DueDate Project");

        var utcDueDate = new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc);

        var response = await client.PostAsJsonAsync(
            "/api/v1/tasks",
            new CreateTaskCommand(projectId, "Explicit UTC DueDate Task", null, "Medium", null, utcDueDate));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var createdTask = await response.Content.ReadFromJsonAsync<TaskItem>();

        Assert.NotNull(createdTask);
        Assert.NotNull(createdTask!.DueDate);
        Assert.Equal(utcDueDate, createdTask.DueDate!.Value.ToUniversalTime());
    }

    [Fact]
    public async Task CreateTask_WithLocalKindDueDate_ShouldConvertToUtcInstantNotRelabel()
    {
        using var client = _factory.CreateClient();

        var projectId = await RegisterLoginAndCreateProjectAsync(client, "Local Kind DueDate Project");

        // Constructed with Kind = Local so the handler's NormalizeToUtc must call
        // .ToUniversalTime() (shifting the instant) rather than merely relabeling it as UTC.
        var localDueDate = new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Local);
        var expectedUtcInstant = localDueDate.ToUniversalTime();

        var response = await client.PostAsJsonAsync(
            "/api/v1/tasks",
            new CreateTaskCommand(projectId, "Local Kind DueDate Task", null, "Medium", null, localDueDate));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var createdTask = await response.Content.ReadFromJsonAsync<TaskItem>();

        Assert.NotNull(createdTask);
        Assert.NotNull(createdTask!.DueDate);
        Assert.Equal(expectedUtcInstant, createdTask.DueDate!.Value.ToUniversalTime());
    }

    [Fact]
    public async Task UpdateTask_WithDateOnlyDueDate_ShouldReturnOkNotInternalServerError()
    {
        using var client = _factory.CreateClient();

        var projectId = await RegisterLoginAndCreateProjectAsync(client, "Update Date-Only DueDate Project");
        var taskId = await CreateTaskAsync(client, projectId, "Task To Update (date-only)");

        // Same rationale as the Create-side test above: raw JSON so "dueDate" deserializes with
        // Kind = Unspecified, exactly as a native <input type="date"> would send on an edit form.
        var payload = """
            {"title":"Updated Task","description":null,"status":"Todo","priority":"Medium","assignedUserId":null,"dueDate":"2026-11-07","isBlocked":false}
            """;

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await client.PutAsync($"/api/v1/tasks/{taskId}", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedTask = await response.Content.ReadFromJsonAsync<TaskItem>();

        Assert.NotNull(updatedTask);
        Assert.NotNull(updatedTask!.DueDate);
        Assert.Equal(new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc), updatedTask.DueDate!.Value.ToUniversalTime());
    }

    [Fact]
    public async Task UpdateTask_WithExplicitUtcDueDate_ShouldPersistItUnchanged()
    {
        using var client = _factory.CreateClient();

        var projectId = await RegisterLoginAndCreateProjectAsync(client, "Update Explicit UTC DueDate Project");
        var taskId = await CreateTaskAsync(client, projectId, "Task To Update (UTC)");

        var utcDueDate = new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc);

        var response = await client.PutAsJsonAsync(
            $"/api/v1/tasks/{taskId}",
            new UpdateTaskRequest("Updated Task", null, "Todo", "Medium", null, utcDueDate, false));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedTask = await response.Content.ReadFromJsonAsync<TaskItem>();

        Assert.NotNull(updatedTask);
        Assert.NotNull(updatedTask!.DueDate);
        Assert.Equal(utcDueDate, updatedTask.DueDate!.Value.ToUniversalTime());
    }

    [Fact]
    public async Task UpdateTask_WithLocalKindDueDate_ShouldConvertToUtcInstantNotRelabel()
    {
        using var client = _factory.CreateClient();

        var projectId = await RegisterLoginAndCreateProjectAsync(client, "Update Local Kind DueDate Project");
        var taskId = await CreateTaskAsync(client, projectId, "Task To Update (Local)");

        var localDueDate = new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Local);
        var expectedUtcInstant = localDueDate.ToUniversalTime();

        var response = await client.PutAsJsonAsync(
            $"/api/v1/tasks/{taskId}",
            new UpdateTaskRequest("Updated Task", null, "Todo", "Medium", null, localDueDate, false));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedTask = await response.Content.ReadFromJsonAsync<TaskItem>();

        Assert.NotNull(updatedTask);
        Assert.NotNull(updatedTask!.DueDate);
        Assert.Equal(expectedUtcInstant, updatedTask.DueDate!.Value.ToUniversalTime());
    }
}
