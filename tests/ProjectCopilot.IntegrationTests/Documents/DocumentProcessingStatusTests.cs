using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using ProjectCopilot.Api.Endpoints;
using ProjectCopilot.Application.Common.Exceptions;
using ProjectCopilot.Application.Documents;
using ProjectCopilot.Application.Documents.CreateDocument;
using ProjectCopilot.Application.Documents.TransitionDocumentProcessingStatus;
using ProjectCopilot.Application.Projects.CreateProject;
using ProjectCopilot.Domain.Entities;

namespace ProjectCopilot.IntegrationTests.Documents;

/// <summary>
/// End-to-end coverage of the document processing state machine against the real HTTP pipeline
/// and a real PostgreSQL database: the status a real upload produces, the status every documents
/// response carries, and the transitions observed through the Development-only verification
/// route.
/// </summary>
/// <remarks>
/// The shared factory runs as "Testing", which is <em>not</em> Development — so the processing
/// route does not exist on it. Tests that need the route build a Development host over the same
/// test database; tests that assert the route's absence use the ordinary one.
/// </remarks>
public class DocumentProcessingStatusTests : IClassFixture<ProjectCopilotWebApplicationFactory>
{
    private readonly ProjectCopilotWebApplicationFactory _factory;

    public DocumentProcessingStatusTests(ProjectCopilotWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    // ---------------------------------------------------------------- helpers

    /// <summary>
    /// A host running in Development, where the manual processing route is mapped. It shares the
    /// base factory's configuration, so it uses the same test database and the same JWT signing
    /// key — a token issued by either host is accepted by both.
    /// </summary>
    private WebApplicationFactory<Program> CreateDevelopmentFactory() =>
        _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));

    private static void Authenticate(HttpClient client, string accessToken) =>
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

    private static async Task<(HttpClient Client, string Token, Project Project)>
        CreateAuthenticatedProjectAsync(WebApplicationFactory<Program> factory, string projectName)
    {
        var client = factory.CreateClient();
        var (token, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(client);
        Authenticate(client, token);

        var response = await client.PostAsJsonAsync(
            "/api/v1/projects",
            new CreateProjectCommand($"{projectName} {Guid.NewGuid():N}", null, null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var project = await response.Content.ReadFromJsonAsync<Project>();
        Assert.NotNull(project);

        return (client, token, project!);
    }

    private static async Task<DocumentResponse> CreateDocumentAsync(
        HttpClient client,
        Guid projectId,
        string title = "Processing target")
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/documents",
            new CreateDocumentCommand(projectId, title, null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var document = await response.Content.ReadFromJsonAsync<DocumentResponse>(JsonOptions);
        Assert.NotNull(document);

        return document!;
    }

    private static byte[] PdfBytes() => "%PDF-1.7\n1 0 obj\nendobj\n%%EOF\n"u8.ToArray();

    private static async Task<DocumentResponse> UploadFileAsync(HttpClient client, Guid documentId)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(PdfBytes());
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        form.Add(fileContent, "file", "processing.pdf");

        var response = await client.PostAsync($"/api/v1/documents/{documentId}/file", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var document = await response.Content.ReadFromJsonAsync<DocumentResponse>(JsonOptions);
        Assert.NotNull(document);

        return document!;
    }

    /// <summary>Creates a document, attaches a file to it, and returns it in its Uploaded state.</summary>
    private static async Task<DocumentResponse> CreateUploadedDocumentAsync(
        HttpClient client,
        Guid projectId,
        string title = "Processing target")
    {
        var document = await CreateDocumentAsync(client, projectId, title);

        return await UploadFileAsync(client, document.Id);
    }

    private static Task<HttpResponseMessage> ProcessingActionAsync(
        HttpClient client,
        Guid documentId,
        string? action) =>
        client.PostAsJsonAsync(
            $"/api/v1/documents/{documentId}/processing",
            new DocumentProcessingActionRequest(action));

    private static async Task<DocumentResponse> GetDocumentAsync(HttpClient client, Guid documentId)
    {
        var document = await client.GetFromJsonAsync<DocumentResponse>(
            $"/api/v1/documents/{documentId}",
            JsonOptions);

        Assert.NotNull(document);

        return document!;
    }

    // ---------------------------------------------------------------- status on the real upload path

    [Fact]
    public async Task Upload_ShouldPutTheDocumentInTheUploadedStatus()
    {
        var (client, _, project) = await CreateAuthenticatedProjectAsync(_factory, "Status After Upload");
        using var clientLifetime = client;

        var created = await CreateDocumentAsync(client, project.Id);

        // A document with no file has no processing status at all — that is the absence of one,
        // not a state of its own.
        Assert.Null(created.Status);

        var uploaded = await UploadFileAsync(client, created.Id);
        Assert.Equal(DocumentProcessingStatuses.Uploaded, uploaded.Status);

        // ...and it was genuinely persisted, not just echoed back by the upload response.
        var reloaded = await GetDocumentAsync(client, created.Id);
        Assert.Equal(DocumentProcessingStatuses.Uploaded, reloaded.Status);
    }

    [Fact]
    public async Task EveryDocumentsResponse_ShouldExposeTheProcessingStatus()
    {
        var (client, _, project) = await CreateAuthenticatedProjectAsync(_factory, "Status On Every Response");
        using var clientLifetime = client;

        var withFile = await CreateUploadedDocumentAsync(client, project.Id, "With file");
        var withoutFile = await CreateDocumentAsync(client, project.Id, "Without file");

        // GET by id
        Assert.Equal(DocumentProcessingStatuses.Uploaded, (await GetDocumentAsync(client, withFile.Id)).Status);
        Assert.Null((await GetDocumentAsync(client, withoutFile.Id)).Status);

        // PUT, which must not disturb the processing status
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/v1/documents/{withFile.Id}",
            new UpdateDocumentRequest("Renamed", "Edited"));

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updated = await updateResponse.Content.ReadFromJsonAsync<DocumentResponse>(JsonOptions);
        Assert.Equal(DocumentProcessingStatuses.Uploaded, updated!.Status);

        // GET list
        var list = await client.GetFromJsonAsync<List<DocumentResponse>>(
            $"/api/v1/documents?projectId={project.Id}",
            JsonOptions);

        Assert.NotNull(list);
        Assert.Equal(
            DocumentProcessingStatuses.Uploaded,
            list!.Single(document => document.Id == withFile.Id).Status);
        Assert.Null(list.Single(document => document.Id == withoutFile.Id).Status);

        // The field is actually present on the wire, not merely defaulting to null on the client.
        var rawList = await client.GetStringAsync($"/api/v1/documents?projectId={project.Id}");
        Assert.Contains("\"status\"", rawList, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------- observable transitions

    [Fact]
    public async Task ProcessingRoute_ShouldDriveADocumentFromUploadedThroughProcessingToReady()
    {
        using var developmentFactory = CreateDevelopmentFactory();

        var (client, _, project) =
            await CreateAuthenticatedProjectAsync(developmentFactory, "Processing To Ready");
        using var clientLifetime = client;

        var document = await CreateUploadedDocumentAsync(client, project.Id);
        Assert.Equal(DocumentProcessingStatuses.Uploaded, document.Status);

        var started = await ProcessingActionAsync(client, document.Id, "Start");
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        Assert.Equal(
            DocumentProcessingStatuses.Processing,
            (await started.Content.ReadFromJsonAsync<DocumentResponse>(JsonOptions))!.Status);
        Assert.Equal(
            DocumentProcessingStatuses.Processing,
            (await GetDocumentAsync(client, document.Id)).Status);

        var succeeded = await ProcessingActionAsync(client, document.Id, "Succeed");
        Assert.Equal(HttpStatusCode.OK, succeeded.StatusCode);
        Assert.Equal(
            DocumentProcessingStatuses.Ready,
            (await GetDocumentAsync(client, document.Id)).Status);
    }

    [Fact]
    public async Task ProcessingRoute_ShouldDriveADocumentFromUploadedThroughProcessingToFailed()
    {
        using var developmentFactory = CreateDevelopmentFactory();

        var (client, _, project) =
            await CreateAuthenticatedProjectAsync(developmentFactory, "Processing To Failed");
        using var clientLifetime = client;

        var document = await CreateUploadedDocumentAsync(client, project.Id);

        Assert.Equal(
            HttpStatusCode.OK,
            (await ProcessingActionAsync(client, document.Id, "Start")).StatusCode);

        var failed = await ProcessingActionAsync(client, document.Id, "Fail");
        Assert.Equal(HttpStatusCode.OK, failed.StatusCode);
        Assert.Equal(
            DocumentProcessingStatuses.Failed,
            (await GetDocumentAsync(client, document.Id)).Status);
    }

    /// <summary>
    /// The two 409s the state machine can produce must stay distinguishable in the response body:
    /// only a lost race is worth retrying unchanged.
    /// </summary>
    [Fact]
    public async Task ProcessingRoute_WithAnIllegalTransition_ShouldReturnTheBusinessRuleConflict()
    {
        using var developmentFactory = CreateDevelopmentFactory();

        var (client, _, project) =
            await CreateAuthenticatedProjectAsync(developmentFactory, "Illegal Transition");
        using var clientLifetime = client;

        var document = await CreateUploadedDocumentAsync(client, project.Id);

        // Succeed before Start: nothing has begun processing.
        var early = await ProcessingActionAsync(client, document.Id, "Succeed");

        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);

        var problem = JsonSerializer.Deserialize<ProblemDetails>(
            await early.Content.ReadAsStringAsync(),
            JsonOptions);

        Assert.Equal(
            TransitionDocumentProcessingStatusHandler.InvalidTransitionMessage,
            problem!.Detail);
        Assert.NotEqual(
            TransitionDocumentProcessingStatusHandler.ConcurrentlyModifiedMessage,
            problem.Detail);

        // The rejected request changed nothing.
        Assert.Equal(
            DocumentProcessingStatuses.Uploaded,
            (await GetDocumentAsync(client, document.Id)).Status);

        // Starting twice is equally rejected.
        Assert.Equal(
            HttpStatusCode.OK,
            (await ProcessingActionAsync(client, document.Id, "Start")).StatusCode);

        var restarted = await ProcessingActionAsync(client, document.Id, "Start");
        Assert.Equal(HttpStatusCode.Conflict, restarted.StatusCode);
        Assert.Equal(
            DocumentProcessingStatuses.Processing,
            (await GetDocumentAsync(client, document.Id)).Status);
    }

    /// <summary>
    /// A document with no uploaded file has nothing to process, so it cannot enter the state
    /// machine at all.
    /// </summary>
    [Fact]
    public async Task ProcessingRoute_OnADocumentWithNoFile_ShouldReturnConflict()
    {
        using var developmentFactory = CreateDevelopmentFactory();

        var (client, _, project) =
            await CreateAuthenticatedProjectAsync(developmentFactory, "Processing Without File");
        using var clientLifetime = client;

        var document = await CreateDocumentAsync(client, project.Id);

        var response = await ProcessingActionAsync(client, document.Id, "Start");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Null((await GetDocumentAsync(client, document.Id)).Status);
    }

    [Theory]
    [InlineData("Restart")]
    [InlineData("Ready")]
    [InlineData("")]
    [InlineData(null)]
    public async Task ProcessingRoute_WithAnUnknownAction_ShouldReturnBadRequest(string? action)
    {
        using var developmentFactory = CreateDevelopmentFactory();

        var (client, _, project) =
            await CreateAuthenticatedProjectAsync(developmentFactory, "Unknown Processing Action");
        using var clientLifetime = client;

        var document = await CreateUploadedDocumentAsync(client, project.Id);

        var response = await ProcessingActionAsync(client, document.Id, action);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            DocumentProcessingStatuses.Uploaded,
            (await GetDocumentAsync(client, document.Id)).Status);
    }

    // ---------------------------------------------------------------- authorization

    [Fact]
    public async Task ProcessingRoute_WithoutAToken_ShouldReturnUnauthorized()
    {
        using var developmentFactory = CreateDevelopmentFactory();
        using var client = developmentFactory.CreateClient();

        var response = await ProcessingActionAsync(client, Guid.NewGuid(), "Start");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Being Development-only buys this route no authorization shortcut: a non-member must get
    /// exactly what they would get for an id that never existed, and must not be able to move
    /// another project's document.
    /// </summary>
    [Fact]
    public async Task ProcessingRoute_AsNonMemberOrForUnknownId_ShouldReturnIdenticalNotFound()
    {
        using var developmentFactory = CreateDevelopmentFactory();

        var (ownerClient, _, ownerProject) =
            await CreateAuthenticatedProjectAsync(developmentFactory, "Processing Foreign Project");
        using var ownerLifetime = ownerClient;

        var foreignDocument = await CreateUploadedDocumentAsync(ownerClient, ownerProject.Id);

        using var outsiderClient = developmentFactory.CreateClient();
        var (outsiderToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(outsiderClient);
        Authenticate(outsiderClient, outsiderToken);

        var foreignResponse = await ProcessingActionAsync(outsiderClient, foreignDocument.Id, "Start");
        var unknownResponse = await ProcessingActionAsync(outsiderClient, Guid.NewGuid(), "Start");

        Assert.Equal(HttpStatusCode.NotFound, foreignResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownResponse.StatusCode);

        var foreignBody = await foreignResponse.Content.ReadAsStringAsync();
        var unknownBody = await unknownResponse.Content.ReadAsStringAsync();

        var foreignProblem = JsonSerializer.Deserialize<ProblemDetails>(foreignBody, JsonOptions);
        var unknownProblem = JsonSerializer.Deserialize<ProblemDetails>(unknownBody, JsonOptions);

        Assert.Equal(NotFoundException.HiddenResourceMessage, foreignProblem!.Detail);
        Assert.Equal(unknownProblem!.Detail, foreignProblem.Detail);

        // Neither body may disclose the foreign project's or document's real id.
        Assert.DoesNotContain(ownerProject.Id.ToString(), foreignBody);
        Assert.DoesNotContain(foreignDocument.Id.ToString(), foreignBody);
        Assert.DoesNotContain(ownerProject.Id.ToString(), unknownBody);

        // And the outsider's attempt left the document untouched.
        Assert.Equal(
            DocumentProcessingStatuses.Uploaded,
            (await GetDocumentAsync(ownerClient, foreignDocument.Id)).Status);
    }

    /// <summary>
    /// An outsider must not be able to tell a rejected transition from a hidden document, so an
    /// illegal transition on a foreign document is still the same 404, never a 409.
    /// </summary>
    [Fact]
    public async Task ProcessingRoute_AsNonMemberWithAnIllegalTransition_ShouldStillReturnNotFound()
    {
        using var developmentFactory = CreateDevelopmentFactory();

        var (ownerClient, _, ownerProject) =
            await CreateAuthenticatedProjectAsync(developmentFactory, "Processing Foreign Illegal");
        using var ownerLifetime = ownerClient;

        var foreignDocument = await CreateUploadedDocumentAsync(ownerClient, ownerProject.Id);

        using var outsiderClient = developmentFactory.CreateClient();
        var (outsiderToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(outsiderClient);
        Authenticate(outsiderClient, outsiderToken);

        // "Succeed" from Uploaded is illegal — a member would get a 409.
        var response = await ProcessingActionAsync(outsiderClient, foreignDocument.Id, "Succeed");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------------------------------------------------------------- Development-only registration

    /// <summary>
    /// Outside Development the processing route is not registered at all. This asserts routing
    /// level absence — an empty 404 with no problem body — rather than an authorization 404,
    /// which always carries the hidden-resource detail. The same request against a Development
    /// host succeeds, so the difference is the environment and nothing else.
    /// </summary>
    [Fact]
    public async Task ProcessingRoute_WhenTheHostIsNotDevelopment_ShouldNotBeRoutedAtAll()
    {
        // The shared factory runs as "Testing".
        var (client, token, project) =
            await CreateAuthenticatedProjectAsync(_factory, "Processing Route Absence");
        using var clientLifetime = client;

        var document = await CreateUploadedDocumentAsync(client, project.Id);

        var response = await ProcessingActionAsync(client, document.Id, "Start");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // No body at all: nothing handled this request, so no ProblemDetails was produced. An
        // authorization 404 from the documents endpoints always carries a detail message.
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(string.IsNullOrEmpty(body), $"Expected an empty routing 404 body but got: {body}");
        Assert.DoesNotContain(NotFoundException.HiddenResourceMessage, body);

        // Nothing moved.
        Assert.Equal(
            DocumentProcessingStatuses.Uploaded,
            (await GetDocumentAsync(client, document.Id)).Status);

        // The very same request, by the very same user, on a Development host: the route exists
        // there, so the 404 above was the route's absence rather than a rejection.
        using var developmentFactory = CreateDevelopmentFactory();
        using var developmentClient = developmentFactory.CreateClient();
        Authenticate(developmentClient, token);

        var developmentResponse = await ProcessingActionAsync(developmentClient, document.Id, "Start");

        Assert.Equal(HttpStatusCode.OK, developmentResponse.StatusCode);
        Assert.Equal(
            DocumentProcessingStatuses.Processing,
            (await GetDocumentAsync(client, document.Id)).Status);
    }

    /// <summary>
    /// The production documents routes are unaffected by the Development-only registration — the
    /// gate must not accidentally move anything else behind it.
    /// </summary>
    [Fact]
    public async Task ProductionDocumentRoutes_ShouldStillWorkOutsideDevelopment()
    {
        var (client, _, project) =
            await CreateAuthenticatedProjectAsync(_factory, "Production Routes Unaffected");
        using var clientLifetime = client;

        var document = await CreateUploadedDocumentAsync(client, project.Id);

        Assert.Equal(DocumentProcessingStatuses.Uploaded, (await GetDocumentAsync(client, document.Id)).Status);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.DeleteAsync($"/api/v1/documents/{document.Id}")).StatusCode);
    }

    /// <summary>
    /// Deletion stays available in every processing status: Slice 2.2 has nothing actually
    /// processing, so there is nothing to cancel and no reason to lock a document down.
    /// </summary>
    [Fact]
    public async Task Delete_ShouldStillWorkWhileADocumentIsProcessing()
    {
        using var developmentFactory = CreateDevelopmentFactory();

        var (client, _, project) =
            await CreateAuthenticatedProjectAsync(developmentFactory, "Delete While Processing");
        using var clientLifetime = client;

        var document = await CreateUploadedDocumentAsync(client, project.Id);

        Assert.Equal(
            HttpStatusCode.OK,
            (await ProcessingActionAsync(client, document.Id, "Start")).StatusCode);

        var response = await client.DeleteAsync($"/api/v1/documents/{document.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/v1/documents/{document.Id}")).StatusCode);
    }
}
