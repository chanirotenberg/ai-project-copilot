using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using ProjectCopilot.Api.Endpoints;
using ProjectCopilot.Application.Documents.CreateDocument;
using ProjectCopilot.Application.Projects.CreateProject;
using ProjectCopilot.Domain.Entities;

namespace ProjectCopilot.IntegrationTests;

public class DocumentsAuthorizationTests : IClassFixture<ProjectCopilotWebApplicationFactory>
{
    private readonly ProjectCopilotWebApplicationFactory _factory;

    public DocumentsAuthorizationTests(ProjectCopilotWebApplicationFactory factory)
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

    private static async Task<DocumentMetadata> CreateDocumentAsync(
        HttpClient client,
        Guid projectId,
        string title,
        string? description = null)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/documents",
            new CreateDocumentCommand(projectId, title, description));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var document = await response.Content.ReadFromJsonAsync<DocumentMetadata>();
        Assert.NotNull(document);

        return document!;
    }

    [Fact]
    public async Task Member_CanCreateListGetUpdateAndDeleteDocument_ForOwnProject()
    {
        using var client = _factory.CreateClient();

        var (token, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(client);
        var project = await CreateProjectAsync(client, token, "Member Documents Project");

        // Create
        var created = await CreateDocumentAsync(client, project.Id, "Design Doc", "Initial description");
        Assert.Equal(project.Id, created.ProjectId);
        Assert.Equal("Design Doc", created.Title);

        // List
        var listResponse = await client.GetAsync($"/api/v1/documents?projectId={project.Id}");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await listResponse.Content.ReadFromJsonAsync<List<DocumentMetadata>>();
        Assert.NotNull(list);
        Assert.Contains(list!, d => d.Id == created.Id);

        // GetById
        var getResponse = await client.GetAsync($"/api/v1/documents/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<DocumentMetadata>();
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched!.Id);

        // Update
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/v1/documents/{created.Id}",
            new UpdateDocumentRequest("Updated Design Doc", "Updated description"));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<DocumentMetadata>();
        Assert.NotNull(updated);
        Assert.Equal("Updated Design Doc", updated!.Title);
        Assert.Equal("Updated description", updated.Description);

        // Delete
        var deleteResponse = await client.DeleteAsync($"/api/v1/documents/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Confirm gone
        var getAfterDeleteResponse = await client.GetAsync($"/api/v1/documents/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getAfterDeleteResponse.StatusCode);
    }

    [Fact]
    public async Task List_AsNonMember_ShouldReturnNotFound()
    {
        using var ownerClient = _factory.CreateClient();
        using var outsiderClient = _factory.CreateClient();

        var (ownerToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(ownerClient);
        var (outsiderToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(outsiderClient);

        var project = await CreateProjectAsync(ownerClient, ownerToken, "Owner-Only Documents Project");

        Authenticate(outsiderClient, outsiderToken);
        var response = await outsiderClient.GetAsync($"/api/v1/documents?projectId={project.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_AsNonMember_ShouldReturnNotFound()
    {
        using var ownerClient = _factory.CreateClient();
        using var outsiderClient = _factory.CreateClient();

        var (ownerToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(ownerClient);
        var (outsiderToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(outsiderClient);

        var project = await CreateProjectAsync(ownerClient, ownerToken, "Owner-Only Document Detail Project");
        var document = await CreateDocumentAsync(ownerClient, project.Id, "Owner document");

        Authenticate(outsiderClient, outsiderToken);
        var response = await outsiderClient.GetAsync($"/api/v1/documents/{document.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_AsNonMember_ShouldReturnNotFound()
    {
        using var ownerClient = _factory.CreateClient();
        using var outsiderClient = _factory.CreateClient();

        var (ownerToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(ownerClient);
        var (outsiderToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(outsiderClient);

        var project = await CreateProjectAsync(ownerClient, ownerToken, "Owner-Only Update Document Project");
        var document = await CreateDocumentAsync(ownerClient, project.Id, "Owner document to hijack");

        Authenticate(outsiderClient, outsiderToken);
        var response = await outsiderClient.PutAsJsonAsync(
            $"/api/v1/documents/{document.Id}",
            new UpdateDocumentRequest("Hijacked title", null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AsNonMember_ShouldReturnNotFound()
    {
        using var ownerClient = _factory.CreateClient();
        using var outsiderClient = _factory.CreateClient();

        var (ownerToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(ownerClient);
        var (outsiderToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(outsiderClient);

        var project = await CreateProjectAsync(ownerClient, ownerToken, "Owner-Only Delete Document Project");
        var document = await CreateDocumentAsync(ownerClient, project.Id, "Owner document to delete");

        Authenticate(outsiderClient, outsiderToken);
        var response = await outsiderClient.DeleteAsync($"/api/v1/documents/{document.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // Prove it genuinely wasn't deleted: the real owner can still fetch it.
        Authenticate(ownerClient, ownerToken);
        var ownerGetResponse = await ownerClient.GetAsync($"/api/v1/documents/{document.Id}");
        Assert.Equal(HttpStatusCode.OK, ownerGetResponse.StatusCode);
    }

    [Fact]
    public async Task List_ShouldOnlyReturnDocumentsBelongingToQueriedProject()
    {
        using var client = _factory.CreateClient();

        var (token, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(client);
        var projectA = await CreateProjectAsync(client, token, "Project A For List Isolation");
        var projectB = await CreateProjectAsync(client, token, "Project B For List Isolation");

        var docA = await CreateDocumentAsync(client, projectA.Id, "Doc under A");
        var docB = await CreateDocumentAsync(client, projectB.Id, "Doc under B");

        var listAResponse = await client.GetAsync($"/api/v1/documents?projectId={projectA.Id}");
        Assert.Equal(HttpStatusCode.OK, listAResponse.StatusCode);
        var listA = await listAResponse.Content.ReadFromJsonAsync<List<DocumentMetadata>>();

        Assert.NotNull(listA);
        Assert.Contains(listA!, d => d.Id == docA.Id);
        Assert.DoesNotContain(listA!, d => d.Id == docB.Id);
    }

    [Fact]
    public async Task CrossProjectId_DocumentCreatedUnderProjectA_CannotBeAccessedByMemberOfProjectBOnly()
    {
        using var ownerClient = _factory.CreateClient();
        using var otherProjectMemberClient = _factory.CreateClient();

        var (ownerToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(ownerClient);
        var (otherToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(otherProjectMemberClient);

        var projectA = await CreateProjectAsync(ownerClient, ownerToken, "Project A For Cross-Id Isolation");
        var projectB = await CreateProjectAsync(otherProjectMemberClient, otherToken, "Project B For Cross-Id Isolation");

        var docUnderA = await CreateDocumentAsync(ownerClient, projectA.Id, "A-only document");

        // The user is a real member of Project B, but not Project A — the doc's real id
        // must not be reachable just because the caller is an authenticated project member.
        Authenticate(otherProjectMemberClient, otherToken);

        var getResponse = await otherProjectMemberClient.GetAsync($"/api/v1/documents/{docUnderA.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);

        var updateResponse = await otherProjectMemberClient.PutAsJsonAsync(
            $"/api/v1/documents/{docUnderA.Id}",
            new UpdateDocumentRequest("Spoofed update", null));
        Assert.Equal(HttpStatusCode.NotFound, updateResponse.StatusCode);

        var deleteResponse = await otherProjectMemberClient.DeleteAsync($"/api/v1/documents/{docUnderA.Id}");
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Create_WithEmptyTitle_ShouldReturnBadRequest()
    {
        using var client = _factory.CreateClient();

        var (token, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(client);
        var project = await CreateProjectAsync(client, token, "Empty Title Validation Project");

        var response = await client.PostAsJsonAsync(
            "/api/v1/documents",
            new CreateDocumentCommand(project.Id, "", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithTitleOver200Chars_ShouldReturnBadRequest()
    {
        using var client = _factory.CreateClient();

        var (token, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(client);
        var project = await CreateProjectAsync(client, token, "Long Title Validation Project");

        var response = await client.PostAsJsonAsync(
            "/api/v1/documents",
            new CreateDocumentCommand(project.Id, new string('a', 201), null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithDescriptionOver2000Chars_ShouldReturnBadRequest()
    {
        using var client = _factory.CreateClient();

        var (token, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(client);
        var project = await CreateProjectAsync(client, token, "Long Description Validation Project");

        var response = await client.PostAsJsonAsync(
            "/api/v1/documents",
            new CreateDocumentCommand(project.Id, "Valid title", new string('a', 2001)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithEmptyTitle_ShouldReturnBadRequest()
    {
        using var client = _factory.CreateClient();

        var (token, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(client);
        var project = await CreateProjectAsync(client, token, "Update Empty Title Validation Project");
        var document = await CreateDocumentAsync(client, project.Id, "Original title");

        var response = await client.PutAsJsonAsync(
            $"/api/v1/documents/{document.Id}",
            new UpdateDocumentRequest("", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetById_NonexistentDocumentId_ShouldReturnNotFound()
    {
        using var client = _factory.CreateClient();

        var (token, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(client);
        Authenticate(client, token);

        var response = await client.GetAsync($"/api/v1/documents/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_NonexistentDocumentId_ShouldReturnNotFound()
    {
        using var client = _factory.CreateClient();

        var (token, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(client);
        Authenticate(client, token);

        var response = await client.PutAsJsonAsync(
            $"/api/v1/documents/{Guid.NewGuid()}",
            new UpdateDocumentRequest("Does not matter", null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_NonexistentDocumentId_ShouldReturnNotFound()
    {
        using var client = _factory.CreateClient();

        var (token, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(client);
        Authenticate(client, token);

        var response = await client.DeleteAsync($"/api/v1/documents/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithNonexistentProjectId_ShouldReturnNotFound()
    {
        using var client = _factory.CreateClient();

        var (token, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(client);
        Authenticate(client, token);

        var response = await client.PostAsJsonAsync(
            "/api/v1/documents",
            new CreateDocumentCommand(Guid.NewGuid(), "Orphan document", null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_AsNonMember_ShouldReturnNotFound()
    {
        using var ownerClient = _factory.CreateClient();
        using var outsiderClient = _factory.CreateClient();

        var (ownerToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(ownerClient);
        var (outsiderToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(outsiderClient);

        var project = await CreateProjectAsync(ownerClient, ownerToken, "Owner-Only Create Document Project");

        // The project genuinely exists - this proves the membership check (not just
        // the existence check) is what rejects the request, and that both failure
        // modes resolve to the same 404 shape.
        Authenticate(outsiderClient, outsiderToken);
        var response = await outsiderClient.PostAsJsonAsync(
            "/api/v1/documents",
            new CreateDocumentCommand(project.Id, "Should not be created", null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetDocuments_WithoutToken_ShouldReturnUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/documents?projectId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateDocument_WithoutToken_ShouldReturnUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/documents",
            new CreateDocumentCommand(Guid.NewGuid(), "Unauthorized Create Document", null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static readonly JsonSerializerOptions ProblemDetailsJsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Regression test for the information-disclosure finding: GET /api/v1/documents/{id} must
    /// return a 404 with an identical response body whether the id genuinely doesn't exist
    /// (Case A) or belongs to a document in a project the caller isn't a member of (Case B) —
    /// and in neither case may the foreign project's real id, or the foreign document's own id,
    /// appear anywhere in the response body. Before the fix, Case B's message embedded the
    /// document's real ProjectId (derived server-side from the loaded document, not supplied by
    /// the caller), letting an unauthorized caller learn a foreign project's real GUID just by
    /// guessing a document id. Mirrors TasksAuthorizationTests' equivalent test.
    /// </summary>
    [Fact]
    public async Task GetById_NonexistentVsForeignProject_ShouldReturnIdenticalBodyWithNoLeakedIds()
    {
        using var ownerClient = _factory.CreateClient();
        using var outsiderClient = _factory.CreateClient();

        var (ownerToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(ownerClient);
        var foreignProject = await CreateProjectAsync(ownerClient, ownerToken, "Leak-Check Foreign Document Project");
        var foreignDocument = await CreateDocumentAsync(ownerClient, foreignProject.Id, "Leak-Check Foreign Document");

        var (outsiderToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(outsiderClient);
        Authenticate(outsiderClient, outsiderToken);

        // Case A: the id never existed at all.
        var nonexistentResponse = await outsiderClient.GetAsync($"/api/v1/documents/{Guid.NewGuid()}");

        // Case B: the id is real, but belongs to a document under a project the caller is not a
        // member of — the real cross-project-authorization scenario.
        var foreignResponse = await outsiderClient.GetAsync($"/api/v1/documents/{foreignDocument.Id}");

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
        // document's own id, rather than just trusting the message text looks generic.
        Assert.DoesNotContain(foreignProject.Id.ToString(), nonexistentBodyText);
        Assert.DoesNotContain(foreignProject.Id.ToString(), foreignBodyText);
        Assert.DoesNotContain(foreignDocument.Id.ToString(), foreignBodyText);
    }
}
