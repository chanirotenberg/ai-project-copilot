using ProjectCopilot.Application.Common.Exceptions;
using ProjectCopilot.Application.Documents.UpdateDocument;
using ProjectCopilot.Domain.Entities;

namespace ProjectCopilot.Application.Tests.Documents;

/// <summary>
/// Concurrent-edit behaviour of PUT /api/v1/documents/{id}.
/// </summary>
/// <remarks>
/// DocumentMetadata gained an optimistic concurrency token so that concurrent uploads cannot
/// both claim the same document. That token is per-row, so it also applies to ordinary title and
/// description edits: two project members editing the same document around the same time now
/// produce a detected conflict where it used to be a silent last-write-wins. These tests pin
/// that this surfaces as a meaningful 409 rather than an unexpected-failure 500.
/// </remarks>
public class UpdateDocumentHandlerConcurrencyTests
{
    private static readonly Guid MemberUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly FakeDocumentRepository _repository = new();
    private readonly FakeProjectMembershipService _membership = new();

    private UpdateDocumentHandler CreateHandler() => new(
        _repository,
        _membership,
        new UpdateDocumentValidator());

    private DocumentMetadata SeedDocument()
    {
        var now = DateTime.UtcNow.AddMinutes(-5);

        var document = new DocumentMetadata
        {
            Id = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            Title = "Original title",
            Description = "Original description",
            CreatedByUserId = MemberUserId,
            CreatedAt = now,
            UpdatedAt = now
        };

        _repository.Seed(document);
        _membership.AddMember(document.ProjectId, MemberUserId);

        return document;
    }

    private static UpdateDocumentCommand CommandFor(Guid documentId) =>
        new(documentId, "Updated title", "Updated description");

    [Fact]
    public async Task HandleAsync_WhenAnotherWriterEditedFirst_ShouldReportConflictRatherThanAnUnexpectedFailure()
    {
        var document = SeedDocument();

        // What DocumentRepository.SaveChangesAsync raises once PostgreSQL reports that the row's
        // xmin no longer matches the value this request read.
        _repository.SaveChangesException = new ConcurrencyConflictException(
            "The document was modified by another request.",
            new InvalidOperationException("row version mismatch"));

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            CreateHandler().HandleAsync(CommandFor(document.Id), MemberUserId));

        Assert.Equal(UpdateDocumentHandler.ConcurrentlyModifiedMessage, exception.Message);
    }

    /// <summary>
    /// Only a lost concurrency race becomes a 409 — a genuine persistence fault must still be an
    /// unexpected failure, not something a client could read as "just retry".
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenPersistenceFailsForAnyOtherReason_ShouldNotBeReportedAsConflict()
    {
        var document = SeedDocument();
        _repository.SaveChangesException = new InvalidOperationException("database unavailable");

        var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
            CreateHandler().HandleAsync(CommandFor(document.Id), MemberUserId));

        Assert.IsNotType<ConflictException>(exception);
        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public async Task HandleAsync_WhenThereIsNoConflict_ShouldStillUpdateTheDocument()
    {
        var document = SeedDocument();

        var result = await CreateHandler().HandleAsync(CommandFor(document.Id), MemberUserId);

        Assert.Equal("Updated title", result.Title);
        Assert.Equal("Updated description", result.Description);
        Assert.Equal(1, _repository.SaveChangesCallCount);
    }
}
