using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ProjectCopilot.Api.Endpoints;
using ProjectCopilot.Application.Abstractions;
using ProjectCopilot.Application.Common.Exceptions;
using ProjectCopilot.Application.Documents.CreateDocument;
using ProjectCopilot.Application.Projects.CreateProject;
using ProjectCopilot.Domain.Entities;
using ProjectCopilot.Infrastructure.Persistence;
using ProjectCopilot.Infrastructure.Repositories;
using ProjectCopilot.Infrastructure.Storage;

namespace ProjectCopilot.IntegrationTests.Documents;

/// <summary>
/// End-to-end coverage of POST /api/v1/documents/{id}/file and the file-cleanup side of
/// DELETE /api/v1/documents/{id}, against the real HTTP pipeline and a real PostgreSQL database.
/// </summary>
/// <remarks>
/// All upload tests live in this one class on purpose: xUnit runs tests within a class
/// sequentially, so assertions that inspect the shared per-run storage directory cannot race
/// another uploading test. The storage root is a per-run temp directory configured by
/// <see cref="ProjectCopilotWebApplicationFactory"/>, never the development storage location.
/// </remarks>
public class DocumentUploadTests : IClassFixture<ProjectCopilotWebApplicationFactory>
{
    private readonly ProjectCopilotWebApplicationFactory _factory;

    public DocumentUploadTests(ProjectCopilotWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static string StorageRoot => ProjectCopilotWebApplicationFactory.TestDocumentStorageRoot;

    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    // ---------------------------------------------------------------- helpers

    private static void Authenticate(HttpClient client, string accessToken) =>
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

    private static async Task<(HttpClient Client, Project Project)> CreateAuthenticatedProjectAsync(
        WebApplicationFactory<Program> factory,
        string projectName)
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

        return (client, project!);
    }

    private static async Task<DocumentResponse> CreateDocumentAsync(
        HttpClient client,
        Guid projectId,
        string title = "Upload target")
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/documents",
            new CreateDocumentCommand(projectId, title, null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var document = await response.Content.ReadFromJsonAsync<DocumentResponse>(JsonOptions);
        Assert.NotNull(document);

        return document!;
    }

    private static MultipartFormDataContent MultipartFile(
        byte[] content,
        string fileName,
        string declaredContentType = "application/octet-stream",
        string fieldName = "file")
    {
        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(declaredContentType);
        form.Add(fileContent, fieldName, fileName);

        return form;
    }

    private static Task<HttpResponseMessage> UploadAsync(
        HttpClient client,
        Guid documentId,
        byte[] content,
        string fileName,
        string declaredContentType = "application/octet-stream",
        string fieldName = "file") =>
        client.PostAsync(
            $"/api/v1/documents/{documentId}/file",
            MultipartFile(content, fileName, declaredContentType, fieldName));

    private static byte[] PdfBytes() => "%PDF-1.7\n1 0 obj\nendobj\n%%EOF\n"u8.ToArray();

    // A genuine ZIP local file header, as an OOXML (.docx) package begins with.
    private static byte[] DocxBytes() =>
    [
        0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x06, 0x00,
        0x08, 0x00, 0x00, 0x00, 0x21, 0x00, 0x11, 0x22
    ];

    private static byte[] TextBytes() =>
        Encoding.UTF8.GetBytes("Plain text with UTF-8: שלום.\nSecond line.\n");

    private static string[] StoredFileNames() =>
        Directory.Exists(StorageRoot)
            ? Directory.GetFiles(StorageRoot)
                .Select(path => Path.GetFileName(path))
                .Where(name => !string.IsNullOrEmpty(name))
                .ToArray()
            : [];

    // ---------------------------------------------------------------- happy path

    [Theory]
    [InlineData("report.pdf", ".pdf", "application/pdf", "application/pdf")]
    [InlineData("spec.docx", ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("notes.txt", ".txt", "text/plain", "text/plain")]
    // .md and .csv are commonly mislabelled by browsers; the declared type is informational only,
    // so these must still be accepted and recorded under their canonical type.
    [InlineData("readme.md", ".md", "text/plain", "text/markdown")]
    [InlineData("rows.csv", ".csv", "application/octet-stream", "text/csv")]
    public async Task Upload_WithAllowListedTypeAndMatchingContent_ShouldSucceed(
        string fileName,
        string expectedExtension,
        string declaredContentType,
        string expectedStoredContentType)
    {
        var (client, project) = await CreateAuthenticatedProjectAsync(_factory, "Upload Happy Path");
        using var clientLifetime = client;

        var document = await CreateDocumentAsync(client, project.Id);

        var content = expectedExtension switch
        {
            ".pdf" => PdfBytes(),
            ".docx" => DocxBytes(),
            _ => TextBytes()
        };

        var response = await UploadAsync(client, document.Id, content, fileName, declaredContentType);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<DocumentResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal(document.Id, updated!.Id);
        Assert.Equal(fileName, updated.OriginalFileName);
        Assert.Equal(expectedStoredContentType, updated.ContentType);
        Assert.Equal(content.Length, updated.SizeBytes);

        // The file really landed inside the configured storage root, under a name ending in the
        // allow-listed extension.
        var stored = StoredFileNames().Where(name => name.EndsWith(expectedExtension, StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(stored);

        var matching = stored
            .Select(name => Path.Combine(StorageRoot, name))
            .Where(path => File.ReadAllBytes(path).SequenceEqual(content))
            .ToArray();

        Assert.NotEmpty(matching);
    }

    [Fact]
    public async Task Upload_Response_ShouldNotExposeTheStorageFileNameOrAnyPath()
    {
        var (client, project) = await CreateAuthenticatedProjectAsync(_factory, "Upload Response Leak Check");
        using var clientLifetime = client;

        var document = await CreateDocumentAsync(client, project.Id);

        var uploadResponse = await UploadAsync(client, document.Id, PdfBytes(), "leak-check.pdf");
        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);

        var uploadBody = await uploadResponse.Content.ReadAsStringAsync();
        AssertNoStorageDetails(uploadBody);

        var getResponse = await client.GetAsync($"/api/v1/documents/{document.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var getBody = await getResponse.Content.ReadAsStringAsync();
        AssertNoStorageDetails(getBody);

        // The metadata that *is* meant to be public must still be there.
        Assert.Contains("leak-check.pdf", getBody);

        var listResponse = await client.GetAsync($"/api/v1/documents?projectId={project.Id}");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        AssertNoStorageDetails(await listResponse.Content.ReadAsStringAsync());
    }

    private static void AssertNoStorageDetails(string body)
    {
        Assert.DoesNotContain("storageFileName", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(StorageRoot, body, StringComparison.OrdinalIgnoreCase);

        foreach (var storedFileName in StoredFileNames())
        {
            Assert.DoesNotContain(storedFileName, body, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Upload_WithDuplicateOriginalFileNameOnTwoDocuments_ShouldSucceedForBoth()
    {
        var (client, project) = await CreateAuthenticatedProjectAsync(_factory, "Upload Duplicate Names");
        using var clientLifetime = client;

        var first = await CreateDocumentAsync(client, project.Id, "First");
        var second = await CreateDocumentAsync(client, project.Id, "Second");

        var firstResponse = await UploadAsync(client, first.Id, PdfBytes(), "duplicate-name.pdf");
        var secondResponse = await UploadAsync(client, second.Id, PdfBytes(), "duplicate-name.pdf");

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        var firstDocument = await firstResponse.Content.ReadFromJsonAsync<DocumentResponse>(JsonOptions);
        var secondDocument = await secondResponse.Content.ReadFromJsonAsync<DocumentResponse>(JsonOptions);

        Assert.Equal("duplicate-name.pdf", firstDocument!.OriginalFileName);
        Assert.Equal("duplicate-name.pdf", secondDocument!.OriginalFileName);
    }

    /// <summary>
    /// A traversal-style client file name is stored as metadata only. Nothing may be written
    /// outside the storage root, and the name must not influence the on-disk name at all.
    /// </summary>
    [Fact]
    public async Task Upload_WithPathTraversalStyleFileName_ShouldStoreItAsMetadataOnly()
    {
        var (client, project) = await CreateAuthenticatedProjectAsync(_factory, "Upload Traversal Name");
        using var clientLifetime = client;

        var document = await CreateDocumentAsync(client, project.Id);

        var before = StoredFileNames().ToHashSet(StringComparer.Ordinal);

        var response = await UploadAsync(client, document.Id, PdfBytes(), "../../evil.pdf");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<DocumentResponse>(JsonOptions);
        Assert.Equal("../../evil.pdf", updated!.OriginalFileName);

        var added = StoredFileNames().Where(name => !before.Contains(name)).ToArray();
        var addedName = Assert.Single(added);

        Assert.DoesNotContain("evil", addedName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("..", addedName);
        Assert.EndsWith(".pdf", addedName, StringComparison.Ordinal);
        Assert.True(Guid.TryParseExact(addedName[..^4], "N", out _));

        // Nothing escaped the storage root in either direction the name was aiming at.
        var parent = Directory.GetParent(StorageRoot)!;
        Assert.False(File.Exists(Path.Combine(parent.FullName, "evil.pdf")));
        Assert.False(File.Exists(Path.Combine(parent.Parent!.FullName, "evil.pdf")));
    }

    // ---------------------------------------------------------------- authorization

    [Fact]
    public async Task Upload_WithoutToken_ShouldReturnUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await UploadAsync(client, Guid.NewGuid(), PdfBytes(), "report.pdf");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Uploading to a document in a project the caller is not a member of must be
    /// indistinguishable from uploading to an id that never existed — same status and same body.
    /// </summary>
    [Fact]
    public async Task Upload_AsNonMemberOrToUnknownId_ShouldReturnIdenticalNotFound()
    {
        var (ownerClient, ownerProject) =
            await CreateAuthenticatedProjectAsync(_factory, "Upload Foreign Project");
        using var _ = ownerClient;

        var foreignDocument = await CreateDocumentAsync(ownerClient, ownerProject.Id);

        using var outsiderClient = _factory.CreateClient();
        var (outsiderToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(outsiderClient);
        Authenticate(outsiderClient, outsiderToken);

        var foreignResponse =
            await UploadAsync(outsiderClient, foreignDocument.Id, PdfBytes(), "report.pdf");
        var unknownResponse =
            await UploadAsync(outsiderClient, Guid.NewGuid(), PdfBytes(), "report.pdf");

        Assert.Equal(HttpStatusCode.NotFound, foreignResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownResponse.StatusCode);

        var foreignBody = await foreignResponse.Content.ReadAsStringAsync();
        var unknownBody = await unknownResponse.Content.ReadAsStringAsync();

        var foreignProblem = JsonSerializer.Deserialize<ProblemDetails>(foreignBody, JsonOptions);
        var unknownProblem = JsonSerializer.Deserialize<ProblemDetails>(unknownBody, JsonOptions);

        Assert.False(string.IsNullOrEmpty(foreignProblem!.Detail));
        Assert.Equal(unknownProblem!.Detail, foreignProblem.Detail);

        // Neither body may disclose the foreign project's or document's real id.
        Assert.DoesNotContain(ownerProject.Id.ToString(), foreignBody);
        Assert.DoesNotContain(foreignDocument.Id.ToString(), foreignBody);

        // And the rejected upload must have left the document untouched.
        await AssertNoFileAttachedAsync(ownerClient, foreignDocument.Id);
    }

    // ---------------------------------------------------------------- rejection paths

    [Theory]
    [InlineData("payload.exe")]
    [InlineData("payload.pdf.exe")]
    [InlineData("image.png")]
    [InlineData("sheet.xlsx")]
    [InlineData("noextension")]
    public async Task Upload_WithUnsupportedExtension_ShouldReturnBadRequest(string fileName)
    {
        var (client, project) = await CreateAuthenticatedProjectAsync(_factory, "Upload Bad Extension");
        using var clientLifetime = client;

        var document = await CreateDocumentAsync(client, project.Id);

        var response = await UploadAsync(client, document.Id, PdfBytes(), fileName);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoFileAttachedAsync(client, document.Id);
    }

    [Fact]
    public async Task Upload_WithPdfExtensionButNonPdfContent_ShouldReturnBadRequest()
    {
        var (client, project) = await CreateAuthenticatedProjectAsync(_factory, "Upload Fake Pdf");
        using var clientLifetime = client;

        var document = await CreateDocumentAsync(client, project.Id);

        var response = await UploadAsync(
            client,
            document.Id,
            TextBytes(),
            "not-really.pdf",
            "application/pdf");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoFileAttachedAsync(client, document.Id);
    }

    [Fact]
    public async Task Upload_WithDocxExtensionButNonZipContent_ShouldReturnBadRequest()
    {
        var (client, project) = await CreateAuthenticatedProjectAsync(_factory, "Upload Fake Docx");
        using var clientLifetime = client;

        var document = await CreateDocumentAsync(client, project.Id);

        var response = await UploadAsync(
            client,
            document.Id,
            PdfBytes(),
            "not-really.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoFileAttachedAsync(client, document.Id);
    }

    [Fact]
    public async Task Upload_WithBinaryContentLabelledAsText_ShouldReturnBadRequest()
    {
        var (client, project) = await CreateAuthenticatedProjectAsync(_factory, "Upload Binary As Text");
        using var clientLifetime = client;

        var document = await CreateDocumentAsync(client, project.Id);

        byte[] binary = [0x00, 0x01, 0x02, 0x00, 0xFF, 0x7F, 0x03, 0x04];

        var response = await UploadAsync(client, document.Id, binary, "disguised.txt", "text/plain");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoFileAttachedAsync(client, document.Id);
    }

    [Fact]
    public async Task Upload_WithEmptyFile_ShouldReturnBadRequest()
    {
        var (client, project) = await CreateAuthenticatedProjectAsync(_factory, "Upload Empty File");
        using var clientLifetime = client;

        var document = await CreateDocumentAsync(client, project.Id);

        var response = await UploadAsync(client, document.Id, [], "empty.txt", "text/plain");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoFileAttachedAsync(client, document.Id);
    }

    [Fact]
    public async Task Upload_WithNoFilePart_ShouldReturnBadRequest()
    {
        var (client, project) = await CreateAuthenticatedProjectAsync(_factory, "Upload No File Part");
        using var clientLifetime = client;

        var document = await CreateDocumentAsync(client, project.Id);

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("not a file"), "file");

        var response = await client.PostAsync($"/api/v1/documents/{document.Id}/file", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoFileAttachedAsync(client, document.Id);
    }

    [Fact]
    public async Task Upload_WithMoreThanOneFilePart_ShouldReturnBadRequest()
    {
        var (client, project) = await CreateAuthenticatedProjectAsync(_factory, "Upload Two File Parts");
        using var clientLifetime = client;

        var document = await CreateDocumentAsync(client, project.Id);

        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(PdfBytes()), "file", "first.pdf");
        form.Add(new ByteArrayContent(PdfBytes()), "file", "second.pdf");

        var response = await client.PostAsync($"/api/v1/documents/{document.Id}/file", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoFileAttachedAsync(client, document.Id);
    }

    [Fact]
    public async Task Upload_WithWrongFormFieldName_ShouldReturnBadRequest()
    {
        var (client, project) = await CreateAuthenticatedProjectAsync(_factory, "Upload Wrong Field");
        using var clientLifetime = client;

        var document = await CreateDocumentAsync(client, project.Id);

        var response = await UploadAsync(
            client,
            document.Id,
            PdfBytes(),
            "report.pdf",
            fieldName: "document");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoFileAttachedAsync(client, document.Id);
    }

    [Fact]
    public async Task Upload_WithNonMultipartBody_ShouldReturnBadRequest()
    {
        var (client, project) = await CreateAuthenticatedProjectAsync(_factory, "Upload Not Multipart");
        using var clientLifetime = client;

        var document = await CreateDocumentAsync(client, project.Id);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Id}/file",
            new { file = "nope" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoFileAttachedAsync(client, document.Id);
    }

    /// <summary>
    /// An oversized upload is rejected without being stored.
    /// </summary>
    /// <remarks>
    /// Two layers can reject it. Under Kestrel the per-route transport limit cuts the body off
    /// first and the response is a 413 (that mapping is covered by
    /// <see cref="DocumentUploadErrorMappingTests"/>, since TestServer does not implement
    /// <c>IHttpMaxRequestBodySizeFeature</c> and therefore cannot trigger it). Here the
    /// authoritative application-level rule is what rejects the file, so the expected status is
    /// exactly 400 — deliberately asserted rather than accepting "either", so a silent change in
    /// which layer rejects would show up.
    /// </remarks>
    [Fact]
    public async Task Upload_OverTheConfiguredSizeLimit_ShouldBeRejected()
    {
        var (client, project) = await CreateAuthenticatedProjectAsync(_factory, "Upload Oversized");
        using var clientLifetime = client;

        var document = await CreateDocumentAsync(client, project.Id);

        var oversized = new byte[ProjectCopilotWebApplicationFactory.TestMaxUploadSizeBytes + 1];
        PdfBytes().CopyTo(oversized, 0);

        var response = await UploadAsync(client, document.Id, oversized, "huge.pdf", "application/pdf");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await AssertNoFileAttachedAsync(client, document.Id);
    }

    [Fact]
    public async Task Upload_WhenTheDocumentAlreadyHasAFile_ShouldReturnConflict()
    {
        var (client, project) = await CreateAuthenticatedProjectAsync(_factory, "Upload Re-upload Conflict");
        using var clientLifetime = client;

        var document = await CreateDocumentAsync(client, project.Id);

        var first = await UploadAsync(client, document.Id, PdfBytes(), "original.pdf");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var storedBefore = StoredFileNames().ToHashSet(StringComparer.Ordinal);

        var second = await UploadAsync(client, document.Id, DocxBytes(), "replacement.docx");

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        // The conflict must not have written, replaced or removed anything.
        Assert.Equal(storedBefore, StoredFileNames().ToHashSet(StringComparer.Ordinal));

        var current = await client.GetFromJsonAsync<DocumentResponse>(
            $"/api/v1/documents/{document.Id}",
            JsonOptions);

        Assert.Equal("original.pdf", current!.OriginalFileName);
        Assert.Equal("application/pdf", current.ContentType);
    }

    private async Task AssertNoFileAttachedAsync(HttpClient client, Guid documentId)
    {
        var document = await client.GetFromJsonAsync<DocumentResponse>(
            $"/api/v1/documents/{documentId}",
            JsonOptions);

        Assert.NotNull(document);
        Assert.Null(document!.OriginalFileName);
        Assert.Null(document.ContentType);
        Assert.Null(document.SizeBytes);
    }

    // ---------------------------------------------------------------- concurrency

    /// <summary>
    /// Proves the document row's optimistic concurrency token is genuinely wired up against real
    /// PostgreSQL, which is what closes the concurrent-upload race the in-memory re-upload check
    /// cannot see.
    /// </summary>
    /// <remarks>
    /// Driven through two DI scopes rather than two parallel HTTP requests: two scopes give two
    /// independent DbContexts, which is exactly what two concurrent requests get, but with
    /// deterministic ordering. Racing two real uploads would reproduce this only intermittently
    /// and would make the suite flaky. The handler's half of the behaviour — translating the
    /// lost race into a 409 and compensating away the file it already wrote — is covered
    /// deterministically in UploadDocumentFileHandlerTests.
    /// </remarks>
    [Fact]
    public async Task ConcurrentWritesToTheSameDocument_ShouldBeRejectedByTheConcurrencyToken()
    {
        var (client, project) = await CreateAuthenticatedProjectAsync(_factory, "Upload Concurrency Token");
        using var clientLifetime = client;

        var document = await CreateDocumentAsync(client, project.Id);

        using var winnerScope = _factory.Services.CreateScope();
        using var loserScope = _factory.Services.CreateScope();

        var winnerRepository = winnerScope.ServiceProvider.GetRequiredService<IDocumentRepository>();
        var loserRepository = loserScope.ServiceProvider.GetRequiredService<IDocumentRepository>();

        var winner = await winnerRepository.GetByIdAsync(document.Id);
        var loser = await loserRepository.GetByIdAsync(document.Id);

        Assert.NotNull(winner);
        Assert.NotNull(loser);

        // Both read the document while it still had no file — precisely the state in which the
        // handler's re-upload pre-check would wave both requests through.
        Assert.Null(winner!.StorageFileName);
        Assert.Null(loser!.StorageFileName);

        winner.OriginalFileName = "winner.pdf";
        winner.StorageFileName = $"{Guid.NewGuid():N}.pdf";
        await winnerRepository.SaveChangesAsync();

        loser.OriginalFileName = "loser.pdf";
        loser.StorageFileName = $"{Guid.NewGuid():N}.pdf";

        // Translated from EF's DbUpdateConcurrencyException by DocumentRepository, because the
        // Application layer must not reference EF Core.
        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => loserRepository.SaveChangesAsync());

        // Exactly one of the two writes is what the document ended up with.
        var persisted = await client.GetFromJsonAsync<DocumentResponse>(
            $"/api/v1/documents/{document.Id}",
            JsonOptions);

        Assert.Equal("winner.pdf", persisted!.OriginalFileName);
    }

    // ---------------------------------------------------------------- delete + cleanup

    [Fact]
    public async Task Delete_WhenTheDocumentHasAFile_ShouldRemoveTheStoredFileAndLeaveOthersAlone()
    {
        var (client, project) = await CreateAuthenticatedProjectAsync(_factory, "Delete With File");
        using var clientLifetime = client;

        var target = await CreateDocumentAsync(client, project.Id, "To delete");
        var bystander = await CreateDocumentAsync(client, project.Id, "To keep");

        var before = StoredFileNames().ToHashSet(StringComparer.Ordinal);

        Assert.Equal(HttpStatusCode.OK, (await UploadAsync(client, target.Id, PdfBytes(), "target.pdf")).StatusCode);
        var targetFile = Assert.Single(StoredFileNames(), name => !before.Contains(name));

        var afterTarget = StoredFileNames().ToHashSet(StringComparer.Ordinal);

        Assert.Equal(HttpStatusCode.OK, (await UploadAsync(client, bystander.Id, DocxBytes(), "bystander.docx")).StatusCode);
        var bystanderFile = Assert.Single(StoredFileNames(), name => !afterTarget.Contains(name));

        var deleteResponse = await client.DeleteAsync($"/api/v1/documents/{target.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        Assert.False(File.Exists(Path.Combine(StorageRoot, targetFile)));

        // The unrelated document and its file are untouched.
        Assert.True(File.Exists(Path.Combine(StorageRoot, bystanderFile)));
        var bystanderResponse = await client.GetAsync($"/api/v1/documents/{bystander.Id}");
        Assert.Equal(HttpStatusCode.OK, bystanderResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/v1/documents/{target.Id}")).StatusCode);
    }

    [Fact]
    public async Task Delete_WhenTheDocumentHasNoFile_ShouldStillReturnNoContent()
    {
        var (client, project) = await CreateAuthenticatedProjectAsync(_factory, "Delete Without File");
        using var clientLifetime = client;

        var document = await CreateDocumentAsync(client, project.Id);

        var response = await client.DeleteAsync($"/api/v1/documents/{document.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/v1/documents/{document.Id}")).StatusCode);
    }

    /// <summary>
    /// Storage cleanup is best-effort: the committed row deletion is the client-visible outcome,
    /// so a failing cleanup must still return 204 and must never resurrect the metadata.
    /// </summary>
    [Fact]
    public async Task Delete_WhenStorageCleanupFails_ShouldStillReturnNoContentAndKeepTheMetadataDeleted()
    {
        using var failingFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddScoped<IDocumentFileStorage, DeleteFailingDocumentFileStorage>()));

        var (client, project) =
            await CreateAuthenticatedProjectAsync(failingFactory, "Delete Cleanup Failure");
        using var clientLifetime = client;

        var document = await CreateDocumentAsync(client, project.Id);

        var before = StoredFileNames().ToHashSet(StringComparer.Ordinal);
        Assert.Equal(HttpStatusCode.OK, (await UploadAsync(client, document.Id, PdfBytes(), "orphan.pdf")).StatusCode);
        var storedFile = Assert.Single(StoredFileNames(), name => !before.Contains(name));

        var deleteResponse = await client.DeleteAsync($"/api/v1/documents/{document.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // The metadata stays deleted even though the cleanup threw...
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/v1/documents/{document.Id}")).StatusCode);

        // ...and the orphaned file is simply left behind rather than failing the request.
        Assert.True(File.Exists(Path.Combine(StorageRoot, storedFile)));

        File.Delete(Path.Combine(StorageRoot, storedFile));
    }

    /// <summary>
    /// A delete that loses the row's concurrency race must report 409 rather than falling
    /// through to the generic 500 handler, and must leave both the row and its file intact —
    /// this request did not delete anything.
    /// </summary>
    /// <remarks>
    /// The conflict is injected by overriding <see cref="IDocumentRepository"/> for the deleting
    /// client only, mirroring the fake-repository style used for the PUT and upload concurrency
    /// tests. The setup client uses the unmodified factory, so the document and its file are
    /// created normally; both factories share the same test database.
    /// </remarks>
    [Fact]
    public async Task Delete_WhenItLosesTheConcurrencyRace_ShouldReturnConflictAndChangeNothing()
    {
        using var setupClient = _factory.CreateClient();
        var (token, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(setupClient);
        Authenticate(setupClient, token);

        var projectResponse = await setupClient.PostAsJsonAsync(
            "/api/v1/projects",
            new CreateProjectCommand($"Delete Concurrency Race {Guid.NewGuid():N}", null, null));

        Assert.Equal(HttpStatusCode.Created, projectResponse.StatusCode);
        var project = await projectResponse.Content.ReadFromJsonAsync<Project>();
        Assert.NotNull(project);

        var document = await CreateDocumentAsync(setupClient, project!.Id);

        var before = StoredFileNames().ToHashSet(StringComparer.Ordinal);
        Assert.Equal(
            HttpStatusCode.OK,
            (await UploadAsync(setupClient, document.Id, PdfBytes(), "contested.pdf")).StatusCode);
        var storedFile = Assert.Single(StoredFileNames(), name => !before.Contains(name));

        using var conflictingFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddScoped<IDocumentRepository, DeleteConflictingDocumentRepository>()));

        // Same user and same token against the overridden host, so membership is already
        // satisfied and the 409 cannot be confused with a 404 from the authorization check.
        // Both hosts share the test database and JWT settings from the base factory.
        using var conflictingClient = conflictingFactory.CreateClient();
        Authenticate(conflictingClient, token);

        var response = await conflictingClient.DeleteAsync($"/api/v1/documents/{document.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Reload it and try again", await response.Content.ReadAsStringAsync());

        // Nothing was removed: the row survives...
        var stillThere = await setupClient.GetFromJsonAsync<DocumentResponse>(
            $"/api/v1/documents/{document.Id}",
            JsonOptions);
        Assert.Equal("contested.pdf", stillThere!.OriginalFileName);

        // ...and so does its file, which would otherwise be stranded under surviving metadata.
        Assert.True(File.Exists(Path.Combine(StorageRoot, storedFile)));
    }

    /// <summary>
    /// Reads through the real repository so the endpoint's load-and-authorize behaves normally,
    /// but makes the save lose the concurrency race — what EF reports when the row's token no
    /// longer matches, already translated by the real DocumentRepository.
    /// </summary>
    private sealed class DeleteConflictingDocumentRepository : IDocumentRepository
    {
        private readonly DocumentRepository _inner;

        public DeleteConflictingDocumentRepository(AppDbContext dbContext)
        {
            _inner = new DocumentRepository(dbContext);
        }

        public Task<DocumentMetadata?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            _inner.GetByIdAsync(id, cancellationToken);

        public Task<IReadOnlyList<DocumentMetadata>> GetByProjectIdAsync(
            Guid projectId,
            CancellationToken cancellationToken = default) =>
            _inner.GetByProjectIdAsync(projectId, cancellationToken);

        public Task AddAsync(
            DocumentMetadata document,
            CancellationToken cancellationToken = default) =>
            _inner.AddAsync(document, cancellationToken);

        public void Delete(DocumentMetadata document) => _inner.Delete(document);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new ConcurrencyConflictException(
                "The document was modified by another request.",
                new InvalidOperationException("row version mismatch"));
    }

    /// <summary>
    /// Saves through the real local store so the file genuinely exists, but always fails the
    /// delete — simulating a storage backend that cannot complete cleanup.
    /// </summary>
    private sealed class DeleteFailingDocumentFileStorage : IDocumentFileStorage
    {
        private readonly LocalFileSystemDocumentStorage _inner;

        public DeleteFailingDocumentFileStorage(IOptions<DocumentStorageOptions> options)
        {
            _inner = new LocalFileSystemDocumentStorage(options);
        }

        public Task SaveAsync(
            Stream content,
            string storageFileName,
            CancellationToken cancellationToken = default) =>
            _inner.SaveAsync(content, storageFileName, cancellationToken);

        public Task DeleteAsync(
            string storageFileName,
            CancellationToken cancellationToken = default) =>
            throw new IOException("Simulated storage cleanup failure.");
    }
}
