using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using ProjectCopilot.Application.Projects.CreateProject;
using ProjectCopilot.Application.Tasks.CreateTask;
using ProjectCopilot.Domain.Entities;

namespace ProjectCopilot.IntegrationTests;

public class ProjectMembershipAuthorizationTests : IClassFixture<ProjectCopilotWebApplicationFactory>
{
    private readonly ProjectCopilotWebApplicationFactory _factory;

    public ProjectMembershipAuthorizationTests(ProjectCopilotWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static void Authenticate(HttpClient client, string accessToken)
    {
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
    }

    private async Task<Project> CreateProjectAsync(HttpClient client, string accessToken, string name)
    {
        Authenticate(client, accessToken);

        var response = await client.PostAsJsonAsync(
            "/api/v1/projects",
            new CreateProjectCommand(name, null, null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var project = await response.Content.ReadFromJsonAsync<Project>();
        Assert.NotNull(project);

        return project!;
    }

    [Fact]
    public async Task GetProjects_ShouldIsolateListsBetweenUsers()
    {
        using var clientA = _factory.CreateClient();
        using var clientB = _factory.CreateClient();

        var (tokenA, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(clientA);
        var (tokenB, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(clientB);

        var projectA = await CreateProjectAsync(clientA, tokenA, "User A Project");
        var projectB = await CreateProjectAsync(clientB, tokenB, "User B Project");

        Authenticate(clientA, tokenA);
        var listAResponse = await clientA.GetAsync("/api/v1/projects");
        Assert.Equal(HttpStatusCode.OK, listAResponse.StatusCode);
        var listA = await listAResponse.Content.ReadFromJsonAsync<List<Project>>();

        Authenticate(clientB, tokenB);
        var listBResponse = await clientB.GetAsync("/api/v1/projects");
        Assert.Equal(HttpStatusCode.OK, listBResponse.StatusCode);
        var listB = await listBResponse.Content.ReadFromJsonAsync<List<Project>>();

        Assert.NotNull(listA);
        Assert.NotNull(listB);
        Assert.Contains(listA!, p => p.Id == projectA.Id);
        Assert.DoesNotContain(listA!, p => p.Id == projectB.Id);
        Assert.Contains(listB!, p => p.Id == projectB.Id);
        Assert.DoesNotContain(listB!, p => p.Id == projectA.Id);
    }

    [Fact]
    public async Task GetProjectById_AsMember_ShouldSucceed()
    {
        using var client = _factory.CreateClient();

        var (token, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(client);
        var project = await CreateProjectAsync(client, token, "Member Access Project");

        var response = await client.GetAsync($"/api/v1/projects/{project.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetProjectById_AsNonMember_ShouldReturnNotFound()
    {
        using var ownerClient = _factory.CreateClient();
        using var outsiderClient = _factory.CreateClient();

        var (ownerToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(ownerClient);
        var (outsiderToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(outsiderClient);

        var project = await CreateProjectAsync(ownerClient, ownerToken, "Owner-Only Project");

        Authenticate(outsiderClient, outsiderToken);
        var response = await outsiderClient.GetAsync($"/api/v1/projects/{project.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateProject_ShouldLetCreatorImmediatelyFetchIt()
    {
        using var client = _factory.CreateClient();

        var (token, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(client);
        var project = await CreateProjectAsync(client, token, "Creator Fetch Project");

        var getResponse = await client.GetAsync($"/api/v1/projects/{project.Id}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task GetTasks_AsMember_ShouldSucceed()
    {
        using var client = _factory.CreateClient();

        var (token, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(client);
        var project = await CreateProjectAsync(client, token, "Member Tasks Project");

        var response = await client.GetAsync($"/api/v1/tasks?projectId={project.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetTasks_AsNonMember_ShouldReturnNotFound()
    {
        using var ownerClient = _factory.CreateClient();
        using var outsiderClient = _factory.CreateClient();

        var (ownerToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(ownerClient);
        var (outsiderToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(outsiderClient);

        var project = await CreateProjectAsync(ownerClient, ownerToken, "Owner-Only Tasks Project");

        Authenticate(outsiderClient, outsiderToken);
        var response = await outsiderClient.GetAsync($"/api/v1/tasks?projectId={project.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetTaskById_AsMember_ShouldSucceed()
    {
        using var client = _factory.CreateClient();

        var (token, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(client);
        var project = await CreateProjectAsync(client, token, "Member Task Detail Project");

        var createTaskResponse = await client.PostAsJsonAsync(
            "/api/v1/tasks",
            new CreateTaskCommand(project.Id, "A task", null, "Medium", null, null));

        Assert.Equal(HttpStatusCode.Created, createTaskResponse.StatusCode);

        var task = await createTaskResponse.Content.ReadFromJsonAsync<TaskItem>();
        Assert.NotNull(task);

        var response = await client.GetAsync($"/api/v1/tasks/{task!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetTaskById_AsNonMember_ShouldReturnNotFound()
    {
        using var ownerClient = _factory.CreateClient();
        using var outsiderClient = _factory.CreateClient();

        var (ownerToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(ownerClient);
        var (outsiderToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(outsiderClient);

        var project = await CreateProjectAsync(ownerClient, ownerToken, "Owner-Only Task Detail Project");

        var createTaskResponse = await ownerClient.PostAsJsonAsync(
            "/api/v1/tasks",
            new CreateTaskCommand(project.Id, "A task", null, "Medium", null, null));

        Assert.Equal(HttpStatusCode.Created, createTaskResponse.StatusCode);

        var task = await createTaskResponse.Content.ReadFromJsonAsync<TaskItem>();
        Assert.NotNull(task);

        Authenticate(outsiderClient, outsiderToken);
        var response = await outsiderClient.GetAsync($"/api/v1/tasks/{task!.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateTask_AsMember_OnOwnProject_ShouldSucceed()
    {
        using var client = _factory.CreateClient();

        var (token, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(client);
        var project = await CreateProjectAsync(client, token, "Member Create Task Project");

        var response = await client.PostAsJsonAsync(
            "/api/v1/tasks",
            new CreateTaskCommand(project.Id, "New task", null, "Low", null, null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateTask_AsNonMember_WithSomeoneElsesProjectId_ShouldReturnNotFound()
    {
        using var ownerClient = _factory.CreateClient();
        using var outsiderClient = _factory.CreateClient();

        var (ownerToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(ownerClient);
        var (outsiderToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(outsiderClient);

        var project = await CreateProjectAsync(ownerClient, ownerToken, "Owner-Only Create Task Project");

        Authenticate(outsiderClient, outsiderToken);
        var response = await outsiderClient.PostAsJsonAsync(
            "/api/v1/tasks",
            new CreateTaskCommand(project.Id, "Mismatched task", null, "Low", null, null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTask_AsMember_OnOwnProjectsTask_ShouldSucceed()
    {
        using var client = _factory.CreateClient();

        var (token, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(client);
        var project = await CreateProjectAsync(client, token, "Member Update Task Project");

        var createTaskResponse = await client.PostAsJsonAsync(
            "/api/v1/tasks",
            new CreateTaskCommand(project.Id, "Task to update", null, "Medium", null, null));

        var task = await createTaskResponse.Content.ReadFromJsonAsync<TaskItem>();
        Assert.NotNull(task);

        var response = await client.PutAsJsonAsync(
            $"/api/v1/tasks/{task!.Id}",
            new
            {
                title = "Updated title",
                description = (string?)null,
                status = "InProgress",
                priority = "High",
                assignedUserId = (Guid?)null,
                dueDate = (DateTime?)null,
                isBlocked = false
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTask_AsNonMember_ShouldReturnNotFound()
    {
        using var ownerClient = _factory.CreateClient();
        using var outsiderClient = _factory.CreateClient();

        var (ownerToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(ownerClient);
        var (outsiderToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(outsiderClient);

        var project = await CreateProjectAsync(ownerClient, ownerToken, "Owner-Only Update Task Project");

        var createTaskResponse = await ownerClient.PostAsJsonAsync(
            "/api/v1/tasks",
            new CreateTaskCommand(project.Id, "Task owned by owner", null, "Medium", null, null));

        var task = await createTaskResponse.Content.ReadFromJsonAsync<TaskItem>();
        Assert.NotNull(task);

        Authenticate(outsiderClient, outsiderToken);
        var response = await outsiderClient.PutAsJsonAsync(
            $"/api/v1/tasks/{task!.Id}",
            new
            {
                title = "Hijacked title",
                description = (string?)null,
                status = "InProgress",
                priority = "High",
                assignedUserId = (Guid?)null,
                dueDate = (DateTime?)null,
                isBlocked = false
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetProjectById_NonexistentVsForbidden_ShouldReturnIdenticallyShapedNotFound()
    {
        using var ownerClient = _factory.CreateClient();
        using var outsiderClient = _factory.CreateClient();

        var (ownerToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(ownerClient);
        var (outsiderToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(outsiderClient);

        var project = await CreateProjectAsync(ownerClient, ownerToken, "Body-Shape Project");

        Authenticate(outsiderClient, outsiderToken);
        var forbiddenResponse = await outsiderClient.GetAsync($"/api/v1/projects/{project.Id}");

        var nonexistentResponse = await outsiderClient.GetAsync($"/api/v1/projects/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, forbiddenResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, nonexistentResponse.StatusCode);

        Assert.Equal(
            nonexistentResponse.Content.Headers.ContentType?.MediaType,
            forbiddenResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal("application/json", forbiddenResponse.Content.Headers.ContentType?.MediaType);

        var forbiddenBody = await forbiddenResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        var nonexistentBody = await nonexistentResponse.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(forbiddenBody);
        Assert.NotNull(nonexistentBody);
        Assert.False(string.IsNullOrEmpty(forbiddenBody!.Title));
        Assert.Equal(nonexistentBody!.Title, forbiddenBody.Title);
        Assert.Equal(nonexistentBody.Status, forbiddenBody.Status);
    }

    [Fact]
    public async Task GetTaskById_NonexistentVsForbidden_ShouldReturnIdenticallyShapedNotFound()
    {
        using var ownerClient = _factory.CreateClient();
        using var outsiderClient = _factory.CreateClient();

        var (ownerToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(ownerClient);
        var (outsiderToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(outsiderClient);

        var project = await CreateProjectAsync(ownerClient, ownerToken, "Body-Shape Task Project");

        var createTaskResponse = await ownerClient.PostAsJsonAsync(
            "/api/v1/tasks",
            new CreateTaskCommand(project.Id, "Body-shape task", null, "Medium", null, null));

        Assert.Equal(HttpStatusCode.Created, createTaskResponse.StatusCode);

        var task = await createTaskResponse.Content.ReadFromJsonAsync<TaskItem>();
        Assert.NotNull(task);

        Authenticate(outsiderClient, outsiderToken);
        var forbiddenResponse = await outsiderClient.GetAsync($"/api/v1/tasks/{task!.Id}");

        var nonexistentResponse = await outsiderClient.GetAsync($"/api/v1/tasks/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, forbiddenResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, nonexistentResponse.StatusCode);

        Assert.Equal(
            nonexistentResponse.Content.Headers.ContentType?.MediaType,
            forbiddenResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal("application/json", forbiddenResponse.Content.Headers.ContentType?.MediaType);

        var forbiddenBody = await forbiddenResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        var nonexistentBody = await nonexistentResponse.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(forbiddenBody);
        Assert.NotNull(nonexistentBody);
        Assert.False(string.IsNullOrEmpty(forbiddenBody!.Title));
        Assert.Equal(nonexistentBody!.Title, forbiddenBody.Title);
        Assert.Equal(nonexistentBody.Status, forbiddenBody.Status);
    }
}
