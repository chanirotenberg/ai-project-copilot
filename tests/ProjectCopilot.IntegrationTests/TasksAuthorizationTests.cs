using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ProjectCopilot.Api.Endpoints;
using ProjectCopilot.Application.Projects.CreateProject;
using ProjectCopilot.Application.Tasks.CreateTask;
using ProjectCopilot.Domain.Entities;
using ProjectCopilot.Infrastructure.Persistence;

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

    /// <summary>
    /// Inserts a ProjectMember row directly against the test database. There is no members API
    /// yet (Phase 1 scope is project-creator-as-sole-member plus this direct-insert test seam),
    /// so this is how AssignedUserId-membership tests get a second real member onto a project.
    /// </summary>
    private async Task AddProjectMemberAsync(Guid projectId, Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        dbContext.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = projectId,
            UserId = userId,
            CreatedAtUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateTask_WithYesterdayDueDate_ShouldReturnBadRequest()
    {
        using var client = _factory.CreateClient();

        var projectId = await RegisterLoginAndCreateProjectAsync(client, "Yesterday DueDate Project");
        var yesterday = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");

        var payload = $$"""
            {"projectId":"{{projectId}}","title":"Yesterday DueDate Task","description":null,"priority":"Medium","assignedUserId":null,"dueDate":"{{yesterday}}"}
            """;

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/v1/tasks", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateTask_WithTodayDueDate_ShouldReturnCreated()
    {
        using var client = _factory.CreateClient();

        var projectId = await RegisterLoginAndCreateProjectAsync(client, "Today DueDate Project");
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var payload = $$"""
            {"projectId":"{{projectId}}","title":"Today DueDate Task","description":null,"priority":"Medium","assignedUserId":null,"dueDate":"{{today}}"}
            """;

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/v1/tasks", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateTask_WithTomorrowDueDate_ShouldReturnCreated()
    {
        using var client = _factory.CreateClient();

        var projectId = await RegisterLoginAndCreateProjectAsync(client, "Tomorrow DueDate Project");
        var tomorrow = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd");

        var payload = $$"""
            {"projectId":"{{projectId}}","title":"Tomorrow DueDate Task","description":null,"priority":"Medium","assignedUserId":null,"dueDate":"{{tomorrow}}"}
            """;

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/v1/tasks", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
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

    private static readonly JsonSerializerOptions ProblemDetailsJsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Regression test for the information-disclosure finding: GET /api/v1/tasks/{id} must
    /// return a 404 with an identical response body whether the id genuinely doesn't exist
    /// (Case A) or belongs to a task in a project the caller isn't a member of (Case B) — and
    /// in neither case may the foreign project's real id, or the foreign task's own id, appear
    /// anywhere in the response body. Before the fix, Case B's message embedded the task's real
    /// ProjectId (derived server-side from the loaded task, not supplied by the caller), letting
    /// an unauthorized caller learn a foreign project's real GUID just by guessing a task id.
    /// </summary>
    [Fact]
    public async Task GetTaskById_NonexistentVsForeignProject_ShouldReturnIdenticalBodyWithNoLeakedIds()
    {
        using var ownerClient = _factory.CreateClient();
        using var outsiderClient = _factory.CreateClient();

        var foreignProjectId = await RegisterLoginAndCreateProjectAsync(ownerClient, "Leak-Check Foreign Project");
        var foreignTaskId = await CreateTaskAsync(ownerClient, foreignProjectId, "Leak-Check Foreign Task");

        var (outsiderToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(outsiderClient);
        outsiderClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", outsiderToken);

        // Case A: the id never existed at all.
        var nonexistentResponse = await outsiderClient.GetAsync($"/api/v1/tasks/{Guid.NewGuid()}");

        // Case B: the id is real, but belongs to a task under a project the caller is not a
        // member of — the real cross-project-authorization scenario.
        var foreignResponse = await outsiderClient.GetAsync($"/api/v1/tasks/{foreignTaskId}");

        Assert.Equal(HttpStatusCode.NotFound, nonexistentResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignResponse.StatusCode);

        var nonexistentBodyText = await nonexistentResponse.Content.ReadAsStringAsync();
        var foreignBodyText = await foreignResponse.Content.ReadAsStringAsync();

        var nonexistentProblem = JsonSerializer.Deserialize<ProblemDetails>(nonexistentBodyText, ProblemDetailsJsonOptions);
        var foreignProblem = JsonSerializer.Deserialize<ProblemDetails>(foreignBodyText, ProblemDetailsJsonOptions);

        Assert.NotNull(nonexistentProblem);
        Assert.NotNull(foreignProblem);
        Assert.False(string.IsNullOrEmpty(foreignProblem!.Detail));

        // The two 404 bodies must be byte-for-byte identical in their Detail text — proving
        // "doesn't exist" and "exists but forbidden" are genuinely indistinguishable, not merely
        // both 404s with different wording.
        Assert.Equal(nonexistentProblem!.Detail, foreignProblem.Detail);

        // Empirically prove neither body leaks the foreign project's real id, or the foreign
        // task's own id, rather than just trusting the message text looks generic.
        Assert.DoesNotContain(foreignProjectId.ToString(), nonexistentBodyText);
        Assert.DoesNotContain(foreignProjectId.ToString(), foreignBodyText);
        Assert.DoesNotContain(foreignTaskId.ToString(), foreignBodyText);
    }

    // AUDIT-4 regression coverage: AssignedUserId previously accepted any GUID on Create (no
    // validator rule) and was unconditionally overwritten on Update (zero membership check),
    // silently persisting a non-member or entirely nonexistent user as a task's assignee. The
    // fix adds a handler-level membership check (IProjectMembershipService.IsMemberAsync) for
    // any non-null AssignedUserId, on both Create and Update.

    [Fact]
    public async Task CreateTask_WithAssignedUserId_ToRealProjectMember_ShouldReturnCreatedWithThatAssignee()
    {
        using var ownerClient = _factory.CreateClient();
        using var memberClient = _factory.CreateClient();

        var projectId = await RegisterLoginAndCreateProjectAsync(ownerClient, "Assignee Member Project");

        var (_, _, memberUserId, _) = await AuthTestHelper.RegisterAndLoginAsync(memberClient);
        await AddProjectMemberAsync(projectId, memberUserId);

        var response = await ownerClient.PostAsJsonAsync(
            "/api/v1/tasks",
            new CreateTaskCommand(projectId, "Assigned To Member Task", null, "Medium", memberUserId, null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var task = await response.Content.ReadFromJsonAsync<TaskItem>();
        Assert.NotNull(task);
        Assert.Equal(memberUserId, task!.AssignedUserId);
    }

    [Fact]
    public async Task CreateTask_WithAssignedUserId_ToRegisteredNonMember_ShouldReturnBadRequest()
    {
        using var ownerClient = _factory.CreateClient();
        using var outsiderClient = _factory.CreateClient();

        var projectId = await RegisterLoginAndCreateProjectAsync(ownerClient, "Assignee Non-Member Project");

        var (_, _, outsiderUserId, _) = await AuthTestHelper.RegisterAndLoginAsync(outsiderClient);

        var response = await ownerClient.PostAsJsonAsync(
            "/api/v1/tasks",
            new CreateTaskCommand(projectId, "Assigned To Non-Member Task", null, "Medium", outsiderUserId, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("assignedUserId", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateTask_WithAssignedUserId_ToNonexistentGuid_ShouldReturnBadRequest()
    {
        using var ownerClient = _factory.CreateClient();

        var projectId = await RegisterLoginAndCreateProjectAsync(ownerClient, "Assignee Nonexistent Guid Project");

        var response = await ownerClient.PostAsJsonAsync(
            "/api/v1/tasks",
            new CreateTaskCommand(projectId, "Assigned To Nonexistent Task", null, "Medium", Guid.NewGuid(), null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("assignedUserId", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateTask_WithNullAssignedUserId_ShouldReturnCreatedUnassigned()
    {
        using var ownerClient = _factory.CreateClient();

        var projectId = await RegisterLoginAndCreateProjectAsync(ownerClient, "Assignee Null Project");

        var response = await ownerClient.PostAsJsonAsync(
            "/api/v1/tasks",
            new CreateTaskCommand(projectId, "Unassigned Task", null, "Medium", null, null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var task = await response.Content.ReadFromJsonAsync<TaskItem>();
        Assert.NotNull(task);
        Assert.Null(task!.AssignedUserId);
    }

    [Fact]
    public async Task UpdateTask_WithAssignedUserId_ToAnotherRealProjectMember_ShouldReturnOkWithThatAssignee()
    {
        using var ownerClient = _factory.CreateClient();
        using var memberClient = _factory.CreateClient();

        var projectId = await RegisterLoginAndCreateProjectAsync(ownerClient, "Update Assignee Member Project");
        var taskId = await CreateTaskAsync(ownerClient, projectId, "Task To Reassign");

        var (_, _, memberUserId, _) = await AuthTestHelper.RegisterAndLoginAsync(memberClient);
        await AddProjectMemberAsync(projectId, memberUserId);

        var response = await ownerClient.PutAsJsonAsync(
            $"/api/v1/tasks/{taskId}",
            new UpdateTaskRequest("Reassigned Task", null, "Todo", "Medium", memberUserId, null, false));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var task = await response.Content.ReadFromJsonAsync<TaskItem>();
        Assert.NotNull(task);
        Assert.Equal(memberUserId, task!.AssignedUserId);
    }

    [Fact]
    public async Task UpdateTask_WithAssignedUserId_ToNonMemberOrNonexistentId_ShouldReturnBadRequest()
    {
        using var ownerClient = _factory.CreateClient();
        using var outsiderClient = _factory.CreateClient();

        var projectId = await RegisterLoginAndCreateProjectAsync(ownerClient, "Update Assignee Non-Member Project");
        var taskId = await CreateTaskAsync(ownerClient, projectId, "Task To Reassign Invalidly");

        var (_, _, outsiderUserId, _) = await AuthTestHelper.RegisterAndLoginAsync(outsiderClient);

        var nonMemberResponse = await ownerClient.PutAsJsonAsync(
            $"/api/v1/tasks/{taskId}",
            new UpdateTaskRequest("Reassigned Task", null, "Todo", "Medium", outsiderUserId, null, false));

        Assert.Equal(HttpStatusCode.BadRequest, nonMemberResponse.StatusCode);

        var nonexistentResponse = await ownerClient.PutAsJsonAsync(
            $"/api/v1/tasks/{taskId}",
            new UpdateTaskRequest("Reassigned Task", null, "Todo", "Medium", Guid.NewGuid(), null, false));

        Assert.Equal(HttpStatusCode.BadRequest, nonexistentResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateTask_WithNullAssignedUserId_ShouldReturnOkUnassigned()
    {
        using var ownerClient = _factory.CreateClient();
        using var memberClient = _factory.CreateClient();

        var projectId = await RegisterLoginAndCreateProjectAsync(ownerClient, "Update Assignee Unassign Project");
        var (_, _, memberUserId, _) = await AuthTestHelper.RegisterAndLoginAsync(memberClient);
        await AddProjectMemberAsync(projectId, memberUserId);

        var createResponse = await ownerClient.PostAsJsonAsync(
            "/api/v1/tasks",
            new CreateTaskCommand(projectId, "Task To Unassign", null, "Medium", memberUserId, null));
        createResponse.EnsureSuccessStatusCode();
        var task = await createResponse.Content.ReadFromJsonAsync<TaskItem>();
        Assert.NotNull(task);
        Assert.Equal(memberUserId, task!.AssignedUserId);

        var response = await ownerClient.PutAsJsonAsync(
            $"/api/v1/tasks/{task.Id}",
            new UpdateTaskRequest("Unassigned Task", null, "Todo", "Medium", null, null, false));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedTask = await response.Content.ReadFromJsonAsync<TaskItem>();
        Assert.NotNull(updatedTask);
        Assert.Null(updatedTask!.AssignedUserId);
    }

    /// <summary>
    /// QA-1: tasks within a project can share an identical CreatedAt (the demo seed does this
    /// deliberately, by reusing one captured `now` for every task it creates) - without a
    /// deterministic tiebreaker, Postgres has no guaranteed order among tied rows, and an
    /// UPDATE's tuple rewrite can silently change which tied row sorts first/last on the next
    /// read. This forces an exact tie directly against the DB (not relying on timing) and proves
    /// the list order survives both repeated reads and an unrelated update to a middle row.
    /// </summary>
    [Fact]
    public async Task GetTasks_OrderingIsStableAcrossUpdates_EvenWithTiedCreatedAt()
    {
        using var client = _factory.CreateClient();

        var projectId = await RegisterLoginAndCreateProjectAsync(client, "Stable Ordering Project");

        var task1Id = await CreateTaskAsync(client, projectId, "Task One");
        var task2Id = await CreateTaskAsync(client, projectId, "Task Two");
        var task3Id = await CreateTaskAsync(client, projectId, "Task Three");

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tiedCreatedAt = DateTime.UtcNow;

            var tasks = await dbContext.Tasks
                .Where(t => t.ProjectId == projectId)
                .ToListAsync();

            foreach (var task in tasks)
            {
                task.CreatedAt = tiedCreatedAt;
            }

            await dbContext.SaveChangesAsync();
        }

        var firstListResponse = await client.GetAsync($"/api/v1/tasks?projectId={projectId}");
        firstListResponse.EnsureSuccessStatusCode();
        var firstOrder = await firstListResponse.Content.ReadFromJsonAsync<List<TaskItem>>();
        Assert.NotNull(firstOrder);
        Assert.Equal(3, firstOrder!.Count);

        // Repeated GET with no mutation in between must return the identical order - proving
        // determinism, not just "happened to match once."
        var repeatListResponse = await client.GetAsync($"/api/v1/tasks?projectId={projectId}");
        var repeatOrder = await repeatListResponse.Content.ReadFromJsonAsync<List<TaskItem>>();
        Assert.Equal(firstOrder.Select(t => t.Id), repeatOrder!.Select(t => t.Id));

        // Editing the middle task (by current display order) must not move it.
        var middleTaskId = firstOrder[1].Id;
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/v1/tasks/{middleTaskId}",
            new UpdateTaskRequest("Edited Middle Task", null, "Done", "Critical", null, null, false));
        updateResponse.EnsureSuccessStatusCode();

        var afterUpdateResponse = await client.GetAsync($"/api/v1/tasks?projectId={projectId}");
        var afterUpdateOrder = await afterUpdateResponse.Content.ReadFromJsonAsync<List<TaskItem>>();

        Assert.Equal(firstOrder.Select(t => t.Id), afterUpdateOrder!.Select(t => t.Id));
    }

    [Fact]
    public async Task DeleteTask_AsOwner_ShouldReturnNoContentAndTaskNoLongerExists()
    {
        using var client = _factory.CreateClient();

        var projectId = await RegisterLoginAndCreateProjectAsync(client, "Delete Own Task Project");
        var taskId = await CreateTaskAsync(client, projectId, "Task To Delete");

        var deleteResponse = await client.DeleteAsync($"/api/v1/tasks/{taskId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/v1/tasks/{taskId}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteTask_AsNonMember_ShouldReturnNotFoundAndNotActuallyDelete()
    {
        using var ownerClient = _factory.CreateClient();
        using var outsiderClient = _factory.CreateClient();

        var projectId = await RegisterLoginAndCreateProjectAsync(ownerClient, "Owner-Only Delete Task Project");
        var taskId = await CreateTaskAsync(ownerClient, projectId, "Task Owner Keeps");

        var (outsiderToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(outsiderClient);
        outsiderClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", outsiderToken);

        var deleteResponse = await outsiderClient.DeleteAsync($"/api/v1/tasks/{taskId}");
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);

        // Prove it genuinely wasn't deleted: the real owner can still fetch it.
        var ownerGetResponse = await ownerClient.GetAsync($"/api/v1/tasks/{taskId}");
        Assert.Equal(HttpStatusCode.OK, ownerGetResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteTask_NonexistentTaskId_ShouldReturnNotFound()
    {
        using var client = _factory.CreateClient();

        await RegisterLoginAndCreateProjectAsync(client, "Delete Nonexistent Task Project");

        var response = await client.DeleteAsync($"/api/v1/tasks/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteTask_ShouldNotAffectUnrelatedTasks()
    {
        using var client = _factory.CreateClient();

        var projectId = await RegisterLoginAndCreateProjectAsync(client, "Delete One Of Many Tasks Project");
        var keepTask1Id = await CreateTaskAsync(client, projectId, "Keep Task One");
        var deleteTaskId = await CreateTaskAsync(client, projectId, "Delete This Task");
        var keepTask2Id = await CreateTaskAsync(client, projectId, "Keep Task Two");

        var deleteResponse = await client.DeleteAsync($"/api/v1/tasks/{deleteTaskId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var keep1Response = await client.GetAsync($"/api/v1/tasks/{keepTask1Id}");
        var keep2Response = await client.GetAsync($"/api/v1/tasks/{keepTask2Id}");

        Assert.Equal(HttpStatusCode.OK, keep1Response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, keep2Response.StatusCode);

        var listResponse = await client.GetAsync($"/api/v1/tasks?projectId={projectId}");
        var list = await listResponse.Content.ReadFromJsonAsync<List<TaskItem>>();
        Assert.NotNull(list);
        Assert.Equal(2, list!.Count);
        Assert.DoesNotContain(list, t => t.Id == deleteTaskId);
    }

    [Fact]
    public async Task DeleteTask_CrossProjectId_CannotBeDeletedByMemberOfOtherProjectOnly()
    {
        using var ownerClient = _factory.CreateClient();
        using var otherProjectMemberClient = _factory.CreateClient();

        var projectAId = await RegisterLoginAndCreateProjectAsync(ownerClient, "Project A For Delete Cross-Id Isolation");
        var taskUnderAId = await CreateTaskAsync(ownerClient, projectAId, "A-only task");

        await RegisterLoginAndCreateProjectAsync(otherProjectMemberClient, "Project B For Delete Cross-Id Isolation");

        var deleteResponse = await otherProjectMemberClient.DeleteAsync($"/api/v1/tasks/{taskUnderAId}");
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);

        var ownerGetResponse = await ownerClient.GetAsync($"/api/v1/tasks/{taskUnderAId}");
        Assert.Equal(HttpStatusCode.OK, ownerGetResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteTask_WithoutToken_ShouldReturnUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.DeleteAsync($"/api/v1/tasks/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
