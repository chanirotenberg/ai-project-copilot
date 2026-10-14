using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectCopilot.Application.Common.Exceptions;
using ProjectCopilot.Application.Documents;
using ProjectCopilot.Application.Documents.UploadDocumentFile;
using ProjectCopilot.Domain.Entities;

namespace ProjectCopilot.Application.Tests.Documents;

public class UploadDocumentFileHandlerTests
{
    private const long MaxFileSizeBytes = 20L * 1024 * 1024;

    private static readonly Guid MemberUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OutsiderUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly FakeDocumentRepository _repository = new();
    private readonly FakeProjectMembershipService _membership = new();
    private readonly FakeDocumentFileStorage _storage = new();

    private UploadDocumentFileHandler CreateHandler() => new(
        _repository,
        _membership,
        _storage,
        new UploadDocumentFileValidator(MaxFileSizeBytes),
        NullLogger<UploadDocumentFileHandler>.Instance);

    /// <summary>
    /// Seeds a document whose project <see cref="MemberUserId"/> is a member of.
    /// </summary>
    private DocumentMetadata SeedDocument(string? storageFileName = null)
    {
        var now = DateTime.UtcNow.AddMinutes(-5);

        var document = new DocumentMetadata
        {
            Id = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            Title = "Seeded document",
            CreatedByUserId = MemberUserId,
            StorageFileName = storageFileName,
            CreatedAt = now,
            UpdatedAt = now
        };

        _repository.Seed(document);
        _membership.AddMember(document.ProjectId, MemberUserId);

        return document;
    }

    private static UploadDocumentFileCommand CommandFor(
        Guid documentId,
        string fileName,
        byte[] content) =>
        new(documentId, fileName, content.Length, new MemoryStream(content, writable: false));

    [Theory]
    [InlineData("report.pdf", DocumentFileTypes.Pdf, "application/pdf")]
    [InlineData("spec.docx", DocumentFileTypes.Docx, "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("notes.txt", DocumentFileTypes.Txt, "text/plain")]
    [InlineData("readme.md", DocumentFileTypes.Markdown, "text/markdown")]
    [InlineData("rows.csv", DocumentFileTypes.Csv, "text/csv")]
    public async Task HandleAsync_WithMatchingContent_ShouldStoreFileAndMetadata(
        string fileName,
        string extension,
        string expectedContentType)
    {
        var document = SeedDocument();
        var content = DocumentFileSamples.ForExtension(extension);

        var result = await CreateHandler().HandleAsync(
            CommandFor(document.Id, fileName, content),
            MemberUserId);

        Assert.Equal(fileName, result.OriginalFileName);
        Assert.Equal(expectedContentType, result.ContentType);
        Assert.Equal(content.Length, result.SizeBytes);
        Assert.NotNull(result.StorageFileName);
        Assert.EndsWith(extension, result.StorageFileName);

        // A document that has a file always has a processing status, set by the same save.
        Assert.Equal(DocumentProcessingStatuses.Uploaded, result.Status);

        var saved = Assert.Single(_storage.SavedFiles);
        Assert.Equal(result.StorageFileName, saved.Key);
        Assert.Equal(content, saved.Value);
        Assert.Equal(1, _repository.SaveChangesCallCount);
    }

    /// <summary>
    /// The content type recorded is resolved from the validated extension, never taken from
    /// anything the client declared — .md uploaded as text/plain is still stored as text/markdown.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldRecordServerResolvedContentType()
    {
        var document = SeedDocument();

        var result = await CreateHandler().HandleAsync(
            CommandFor(document.Id, "readme.md", DocumentFileSamples.Markdown()),
            MemberUserId);

        Assert.Equal("text/markdown", result.ContentType);
    }

    [Fact]
    public async Task HandleAsync_WhenDocumentDoesNotExist_ShouldThrowHiddenResourceNotFound()
    {
        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateHandler().HandleAsync(
                CommandFor(Guid.NewGuid(), "report.pdf", DocumentFileSamples.Pdf()),
                MemberUserId));

        Assert.Equal(NotFoundException.HiddenResourceMessage, exception.Message);
        Assert.Empty(_storage.SavedFiles);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsNotAProjectMember_ShouldThrowHiddenResourceNotFound()
    {
        var document = SeedDocument();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateHandler().HandleAsync(
                CommandFor(document.Id, "report.pdf", DocumentFileSamples.Pdf()),
                OutsiderUserId));

        Assert.Equal(NotFoundException.HiddenResourceMessage, exception.Message);
        Assert.Empty(_storage.SavedFiles);
        Assert.Null(document.StorageFileName);
        Assert.Equal(0, _repository.SaveChangesCallCount);
    }

    /// <summary>
    /// Membership is always checked against the project of the loaded document, so the caller
    /// has no way to nominate a project they do belong to.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldAuthorizeAgainstTheDocumentsOwnProject()
    {
        var document = SeedDocument();

        await CreateHandler().HandleAsync(
            CommandFor(document.Id, "report.pdf", DocumentFileSamples.Pdf()),
            MemberUserId);

        var call = Assert.Single(_membership.EnsureMemberCalls);
        Assert.Equal(document.ProjectId, call.ProjectId);
        Assert.Equal(MemberUserId, call.UserId);
    }

    /// <summary>
    /// Authorization must be decided before any file inspection, so an unauthorized caller gets
    /// the same 404 regardless of what they uploaded.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenCallerIsNotAMember_ShouldNotFallThroughToValidation()
    {
        var document = SeedDocument();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateHandler().HandleAsync(
                CommandFor(document.Id, "payload.exe", DocumentFileSamples.BinaryBlob()),
                OutsiderUserId));

        Assert.Equal(NotFoundException.HiddenResourceMessage, exception.Message);
    }

    [Fact]
    public async Task HandleAsync_WhenExtensionIsNotAllowListed_ShouldThrowValidationException()
    {
        var document = SeedDocument();

        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateHandler().HandleAsync(
                CommandFor(document.Id, "payload.exe", DocumentFileSamples.Pdf()),
                MemberUserId));

        Assert.Empty(_storage.SavedFiles);
        Assert.Null(document.StorageFileName);
    }

    [Theory]
    // A .pdf whose bytes are not a PDF, and a .docx whose bytes are not a ZIP: the magic-byte
    // signature is authoritative, so the extension cannot talk its way past it.
    [InlineData("fake.pdf")]
    [InlineData("fake.docx")]
    public async Task HandleAsync_WhenMagicBytesDoNotMatchExtension_ShouldThrowValidationException(
        string fileName)
    {
        var document = SeedDocument();

        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateHandler().HandleAsync(
                CommandFor(document.Id, fileName, DocumentFileSamples.Text()),
                MemberUserId));

        Assert.Empty(_storage.SavedFiles);
        Assert.Null(document.StorageFileName);
    }

    [Fact]
    public async Task HandleAsync_WhenDocxContentIsNotAZipArchive_ShouldThrowValidationException()
    {
        var document = SeedDocument();

        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateHandler().HandleAsync(
                CommandFor(document.Id, "fake.docx", DocumentFileSamples.Pdf()),
                MemberUserId));

        Assert.Empty(_storage.SavedFiles);
    }

    [Theory]
    [InlineData("binary.txt")]
    [InlineData("binary.md")]
    [InlineData("binary.csv")]
    public async Task HandleAsync_WhenTextFamilyContentIsBinary_ShouldThrowValidationException(
        string fileName)
    {
        var document = SeedDocument();

        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateHandler().HandleAsync(
                CommandFor(document.Id, fileName, DocumentFileSamples.BinaryBlob()),
                MemberUserId));

        Assert.Empty(_storage.SavedFiles);
        Assert.Null(document.StorageFileName);
    }

    [Fact]
    public async Task HandleAsync_WhenReportedSizeDisagreesWithContentLength_ShouldThrowValidationException()
    {
        var document = SeedDocument();
        var content = DocumentFileSamples.Pdf();

        var command = new UploadDocumentFileCommand(
            document.Id,
            "report.pdf",
            content.Length + 1,
            new MemoryStream(content, writable: false));

        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateHandler().HandleAsync(command, MemberUserId));

        Assert.Empty(_storage.SavedFiles);
    }

    /// <summary>
    /// A traversal-style client file name is kept verbatim as metadata, while the storage name is
    /// a fresh GUID plus the allow-listed extension — nothing from the client reaches the path.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithPathTraversalStyleFileName_ShouldNotLetItReachTheStorageName()
    {
        var document = SeedDocument();

        var result = await CreateHandler().HandleAsync(
            CommandFor(document.Id, "../../evil.pdf", DocumentFileSamples.Pdf()),
            MemberUserId);

        Assert.Equal("../../evil.pdf", result.OriginalFileName);

        var storageFileName = result.StorageFileName;
        Assert.NotNull(storageFileName);
        Assert.DoesNotContain("..", storageFileName);
        Assert.DoesNotContain("/", storageFileName);
        Assert.DoesNotContain("\\", storageFileName);
        Assert.DoesNotContain("evil", storageFileName);
        Assert.Equal(
            storageFileName,
            Path.GetFileName(storageFileName));

        // 32 hex characters from Guid "N" formatting, plus the allow-listed extension.
        Assert.Equal(32 + DocumentFileTypes.Pdf.Length, storageFileName!.Length);
        Assert.True(Guid.TryParseExact(storageFileName[..32], "N", out _));
    }

    [Fact]
    public async Task HandleAsync_ForTwoDocumentsWithTheSameOriginalFileName_ShouldStoreBothSeparately()
    {
        var first = SeedDocument();
        var second = SeedDocument();
        var content = DocumentFileSamples.Pdf();

        var handler = CreateHandler();

        var firstResult = await handler.HandleAsync(
            CommandFor(first.Id, "duplicate.pdf", content),
            MemberUserId);

        var secondResult = await handler.HandleAsync(
            CommandFor(second.Id, "duplicate.pdf", content),
            MemberUserId);

        Assert.Equal("duplicate.pdf", firstResult.OriginalFileName);
        Assert.Equal("duplicate.pdf", secondResult.OriginalFileName);
        Assert.NotEqual(firstResult.StorageFileName, secondResult.StorageFileName);
        Assert.Equal(2, _storage.SavedFiles.Count);
    }

    [Fact]
    public async Task HandleAsync_WhenDocumentAlreadyHasAFile_ShouldThrowConflictAndNotTouchStorage()
    {
        var document = SeedDocument(storageFileName: "existing-file.pdf");

        await Assert.ThrowsAsync<ConflictException>(() =>
            CreateHandler().HandleAsync(
                CommandFor(document.Id, "replacement.pdf", DocumentFileSamples.Pdf()),
                MemberUserId));

        Assert.Empty(_storage.SavedFiles);
        Assert.Empty(_storage.DeletedFileNames);
        Assert.Equal("existing-file.pdf", document.StorageFileName);
        Assert.Equal(0, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenStorageWriteFails_ShouldNotMutateTheDocument()
    {
        var document = SeedDocument();
        _storage.SaveException = new IOException("disk unavailable");

        await Assert.ThrowsAsync<IOException>(() =>
            CreateHandler().HandleAsync(
                CommandFor(document.Id, "report.pdf", DocumentFileSamples.Pdf()),
                MemberUserId));

        Assert.Null(document.OriginalFileName);
        Assert.Null(document.ContentType);
        Assert.Null(document.SizeBytes);
        Assert.Null(document.StorageFileName);
        Assert.Null(document.Status);
        Assert.Equal(0, _repository.SaveChangesCallCount);
        Assert.Empty(_storage.DeletedFileNames);
    }

    [Fact]
    public async Task HandleAsync_WhenPersistenceFailsAfterTheFileIsWritten_ShouldRemoveTheStoredFileOnce()
    {
        var document = SeedDocument();
        _repository.SaveChangesException = new InvalidOperationException("database unavailable");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateHandler().HandleAsync(
                CommandFor(document.Id, "report.pdf", DocumentFileSamples.Pdf()),
                MemberUserId));

        var deleted = Assert.Single(_storage.DeletedFileNames);
        Assert.EndsWith(DocumentFileTypes.Pdf, deleted);
        Assert.Empty(_storage.SavedFiles);
    }

    /// <summary>
    /// The race the row's concurrency token exists to close: two uploads both read a file-less
    /// document and both get past the re-upload pre-check, so the loser is only detected at save
    /// time. It must be reported as the ordinary 409, not a 500, and its already-written file
    /// must be compensated away rather than left orphaned.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenItLosesTheConcurrencyRace_ShouldConflictAndRemoveItsOwnFile()
    {
        var document = SeedDocument();

        // What DocumentRepository.SaveChangesAsync raises when PostgreSQL reports that the row's
        // xmin no longer matches the value this request read.
        _repository.SaveChangesException = new ConcurrencyConflictException(
            "The document was modified by another request.",
            new InvalidOperationException("row version mismatch"));

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            CreateHandler().HandleAsync(
                CommandFor(document.Id, "racing.pdf", DocumentFileSamples.Pdf()),
                MemberUserId));

        // Indistinguishable from arriving second the ordinary way.
        Assert.Equal(UploadDocumentFileHandler.DocumentAlreadyHasFileMessage, exception.Message);

        var deleted = Assert.Single(_storage.DeletedFileNames);
        Assert.EndsWith(DocumentFileTypes.Pdf, deleted);
        Assert.Empty(_storage.SavedFiles);
    }

    /// <summary>
    /// The losing upload must not be reported as a 409 that the client could mistake for its own
    /// write having been stored: a non-concurrency save failure is still a 500-shaped failure.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenPersistenceFailsForAnyOtherReason_ShouldNotBeReportedAsConflict()
    {
        var document = SeedDocument();
        _repository.SaveChangesException = new InvalidOperationException("database unavailable");

        var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
            CreateHandler().HandleAsync(
                CommandFor(document.Id, "report.pdf", DocumentFileSamples.Pdf()),
                MemberUserId));

        Assert.IsNotType<ConflictException>(exception);
        Assert.IsType<InvalidOperationException>(exception);
    }

    /// <summary>
    /// A failing compensating delete must not mask the original persistence failure — the caller
    /// still sees that, rather than an IO error that would misrepresent what went wrong.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenCompensatingDeleteAlsoFails_ShouldStillSurfaceThePersistenceFailure()
    {
        var document = SeedDocument();
        _repository.SaveChangesException = new InvalidOperationException("database unavailable");
        _storage.DeleteException = new IOException("cleanup failed");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateHandler().HandleAsync(
                CommandFor(document.Id, "report.pdf", DocumentFileSamples.Pdf()),
                MemberUserId));

        Assert.Equal("database unavailable", exception.Message);
        Assert.Single(_storage.DeletedFileNames);
    }
}
