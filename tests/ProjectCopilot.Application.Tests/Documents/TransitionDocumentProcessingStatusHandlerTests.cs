using FluentValidation;
using ProjectCopilot.Application.Common.Exceptions;
using ProjectCopilot.Application.Documents;
using ProjectCopilot.Application.Documents.TransitionDocumentProcessingStatus;
using ProjectCopilot.Application.Documents.UpdateDocument;
using ProjectCopilot.Domain.Entities;

namespace ProjectCopilot.Application.Tests.Documents;

/// <summary>
/// The document processing state machine: which moves are permitted, who may make them, and how
/// the two different 409s it can produce stay distinguishable.
/// </summary>
public class TransitionDocumentProcessingStatusHandlerTests
{
    private static readonly Guid MemberUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OutsiderUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly FakeDocumentRepository _repository = new();
    private readonly FakeProjectMembershipService _membership = new();

    private TransitionDocumentProcessingStatusHandler CreateHandler() => new(
        _repository,
        _membership,
        new TransitionDocumentProcessingStatusValidator());

    /// <summary>
    /// Seeds a document in <paramref name="status"/> whose project <see cref="MemberUserId"/>
    /// belongs to. A null status is a document with no uploaded file.
    /// </summary>
    private DocumentMetadata SeedDocument(string? status)
    {
        var now = DateTime.UtcNow.AddMinutes(-5);

        var document = new DocumentMetadata
        {
            Id = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            Title = "Seeded document",
            CreatedByUserId = MemberUserId,
            StorageFileName = status is null ? null : $"{Guid.NewGuid():N}.pdf",
            Status = status,
            CreatedAt = now,
            UpdatedAt = now
        };

        _repository.Seed(document);
        _membership.AddMember(document.ProjectId, MemberUserId);

        return document;
    }

    // ---------------------------------------------------------------- legal transitions

    [Theory]
    [InlineData(DocumentProcessingStatuses.Uploaded, DocumentProcessingStatuses.Processing)]
    [InlineData(DocumentProcessingStatuses.Processing, DocumentProcessingStatuses.Ready)]
    [InlineData(DocumentProcessingStatuses.Processing, DocumentProcessingStatuses.Failed)]
    public async Task HandleAsync_WithALegalTransition_ShouldMoveTheDocumentAndPersistItOnce(
        string from,
        string to)
    {
        var document = SeedDocument(from);

        var result = await CreateHandler().HandleAsync(
            new TransitionDocumentProcessingStatusCommand(document.Id, to),
            MemberUserId);

        Assert.Equal(to, result.Status);
        Assert.Equal(to, document.Status);
        Assert.Equal(1, _repository.SaveChangesCallCount);
    }

    /// <summary>
    /// The full lifecycle, in the order a real processor would drive it.
    /// </summary>
    [Theory]
    [InlineData(DocumentProcessingStatuses.Ready)]
    [InlineData(DocumentProcessingStatuses.Failed)]
    public async Task HandleAsync_DrivenEndToEnd_ShouldReachTheTerminalStatus(string outcome)
    {
        var document = SeedDocument(DocumentProcessingStatuses.Uploaded);
        var handler = CreateHandler();

        await handler.HandleAsync(
            new TransitionDocumentProcessingStatusCommand(
                document.Id,
                DocumentProcessingStatuses.Processing),
            MemberUserId);

        Assert.Equal(DocumentProcessingStatuses.Processing, document.Status);

        await handler.HandleAsync(
            new TransitionDocumentProcessingStatusCommand(document.Id, outcome),
            MemberUserId);

        Assert.Equal(outcome, document.Status);
        Assert.Equal(2, _repository.SaveChangesCallCount);
    }

    /// <summary>
    /// Nothing may change a document's processing status except this handler, so the only moves
    /// that exist are the three listed here.
    /// </summary>
    [Fact]
    public void IsLegalTransition_ShouldPermitExactlyTheThreeDefinedMoves()
    {
        var statuses = new string?[]
        {
            null,
            DocumentProcessingStatuses.Uploaded,
            DocumentProcessingStatuses.Processing,
            DocumentProcessingStatuses.Ready,
            DocumentProcessingStatuses.Failed
        };

        var legal = (
            from current in statuses
            from target in DocumentProcessingStatuses.All
            where TransitionDocumentProcessingStatusHandler.IsLegalTransition(current, target)
            select $"{current ?? "null"}->{target}").ToList();

        Assert.Equal(
            new[]
            {
                $"{DocumentProcessingStatuses.Uploaded}->{DocumentProcessingStatuses.Processing}",
                $"{DocumentProcessingStatuses.Processing}->{DocumentProcessingStatuses.Ready}",
                $"{DocumentProcessingStatuses.Processing}->{DocumentProcessingStatuses.Failed}"
            },
            legal);
    }

    // ---------------------------------------------------------------- illegal transitions

    [Theory]
    // A document with no uploaded file has nothing to process.
    [InlineData(null, DocumentProcessingStatuses.Processing)]
    [InlineData(null, DocumentProcessingStatuses.Ready)]
    [InlineData(null, DocumentProcessingStatuses.Failed)]
    [InlineData(null, DocumentProcessingStatuses.Uploaded)]
    // An outcome may only be reported by something that actually started processing.
    [InlineData(DocumentProcessingStatuses.Uploaded, DocumentProcessingStatuses.Ready)]
    [InlineData(DocumentProcessingStatuses.Uploaded, DocumentProcessingStatuses.Failed)]
    [InlineData(DocumentProcessingStatuses.Uploaded, DocumentProcessingStatuses.Uploaded)]
    // Processing is not restartable, and it has exactly two outcomes.
    [InlineData(DocumentProcessingStatuses.Processing, DocumentProcessingStatuses.Processing)]
    [InlineData(DocumentProcessingStatuses.Processing, DocumentProcessingStatuses.Uploaded)]
    // Both terminal statuses are terminal in this slice: no reprocess, no retry, no reset.
    [InlineData(DocumentProcessingStatuses.Ready, DocumentProcessingStatuses.Processing)]
    [InlineData(DocumentProcessingStatuses.Ready, DocumentProcessingStatuses.Ready)]
    [InlineData(DocumentProcessingStatuses.Ready, DocumentProcessingStatuses.Failed)]
    [InlineData(DocumentProcessingStatuses.Ready, DocumentProcessingStatuses.Uploaded)]
    [InlineData(DocumentProcessingStatuses.Failed, DocumentProcessingStatuses.Processing)]
    [InlineData(DocumentProcessingStatuses.Failed, DocumentProcessingStatuses.Ready)]
    [InlineData(DocumentProcessingStatuses.Failed, DocumentProcessingStatuses.Failed)]
    [InlineData(DocumentProcessingStatuses.Failed, DocumentProcessingStatuses.Uploaded)]
    public async Task HandleAsync_WithAnIllegalTransition_ShouldConflictAndChangeNothing(
        string? from,
        string to)
    {
        var document = SeedDocument(from);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            CreateHandler().HandleAsync(
                new TransitionDocumentProcessingStatusCommand(document.Id, to),
                MemberUserId));

        // The business-rule 409, never the concurrency one: this request was not permitted at
        // all, so retrying it unchanged would fail again.
        Assert.Equal(
            TransitionDocumentProcessingStatusHandler.InvalidTransitionMessage,
            exception.Message);
        Assert.NotEqual(
            TransitionDocumentProcessingStatusHandler.ConcurrentlyModifiedMessage,
            exception.Message);

        Assert.Equal(from, document.Status);
        Assert.Equal(0, _repository.SaveChangesCallCount);
    }

    /// <summary>
    /// An unknown status string is a malformed request rather than a rejected transition, so it
    /// is a 400 and never reaches the state machine.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Done")]
    [InlineData("uploaded")]
    [InlineData("'; DROP TABLE \"Documents\"; --")]
    public async Task HandleAsync_WithAnUnknownTargetStatus_ShouldThrowValidationException(
        string target)
    {
        var document = SeedDocument(DocumentProcessingStatuses.Uploaded);

        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateHandler().HandleAsync(
                new TransitionDocumentProcessingStatusCommand(document.Id, target),
                MemberUserId));

        Assert.Equal(DocumentProcessingStatuses.Uploaded, document.Status);
        Assert.Equal(0, _repository.SaveChangesCallCount);
    }

    // ---------------------------------------------------------------- authorization

    [Fact]
    public async Task HandleAsync_WhenTheDocumentDoesNotExist_ShouldThrowHiddenResourceNotFound()
    {
        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateHandler().HandleAsync(
                new TransitionDocumentProcessingStatusCommand(
                    Guid.NewGuid(),
                    DocumentProcessingStatuses.Processing),
                MemberUserId));

        Assert.Equal(NotFoundException.HiddenResourceMessage, exception.Message);
        Assert.Equal(0, _repository.SaveChangesCallCount);
    }

    /// <summary>
    /// A non-member must get exactly what they would get for an id that never existed — same
    /// exception, same message — so the route cannot be used to discover which documents exist.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenCallerIsNotAProjectMember_ShouldThrowTheIdenticalNotFound()
    {
        var document = SeedDocument(DocumentProcessingStatuses.Uploaded);

        var foreign = await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateHandler().HandleAsync(
                new TransitionDocumentProcessingStatusCommand(
                    document.Id,
                    DocumentProcessingStatuses.Processing),
                OutsiderUserId));

        var unknown = await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateHandler().HandleAsync(
                new TransitionDocumentProcessingStatusCommand(
                    Guid.NewGuid(),
                    DocumentProcessingStatuses.Processing),
                OutsiderUserId));

        Assert.Equal(unknown.Message, foreign.Message);
        Assert.Equal(NotFoundException.HiddenResourceMessage, foreign.Message);

        // The rejected request left the document exactly as it was.
        Assert.Equal(DocumentProcessingStatuses.Uploaded, document.Status);
        Assert.Equal(0, _repository.SaveChangesCallCount);
    }

    /// <summary>
    /// Membership is checked against the project of the loaded document, so a caller has no way
    /// to nominate a project they do belong to.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldAuthorizeAgainstTheDocumentsOwnProject()
    {
        var document = SeedDocument(DocumentProcessingStatuses.Uploaded);

        await CreateHandler().HandleAsync(
            new TransitionDocumentProcessingStatusCommand(
                document.Id,
                DocumentProcessingStatuses.Processing),
            MemberUserId);

        var call = Assert.Single(_membership.EnsureMemberCalls);
        Assert.Equal(document.ProjectId, call.ProjectId);
        Assert.Equal(MemberUserId, call.UserId);
    }

    /// <summary>
    /// Authorization is decided before the transition rules, so an unauthorized caller cannot
    /// learn a document's current status from whether they get a 404 or a 409.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenCallerIsNotAMember_ShouldNotFallThroughToTheTransitionRules()
    {
        // Ready -> Ready would be a 409 for a member.
        var document = SeedDocument(DocumentProcessingStatuses.Ready);

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateHandler().HandleAsync(
                new TransitionDocumentProcessingStatusCommand(
                    document.Id,
                    DocumentProcessingStatuses.Ready),
                OutsiderUserId));

        Assert.Equal(NotFoundException.HiddenResourceMessage, exception.Message);
    }

    // ---------------------------------------------------------------- concurrency

    /// <summary>
    /// Two transitions out of the same status can both pass the rule check before either saves;
    /// the row's concurrency token settles it. The loser must get the generic "someone else
    /// changed this" 409, which is worded differently from the business-rule 409 precisely
    /// because only this one is worth retrying.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenItLosesTheConcurrencyRace_ShouldThrowTheConcurrencyConflict()
    {
        var document = SeedDocument(DocumentProcessingStatuses.Uploaded);

        // What DocumentRepository.SaveChangesAsync raises once PostgreSQL reports that the row's
        // xmin no longer matches the value this request read.
        _repository.SaveChangesException = new ConcurrencyConflictException(
            "The document was modified by another request.",
            new InvalidOperationException("row version mismatch"));

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            CreateHandler().HandleAsync(
                new TransitionDocumentProcessingStatusCommand(
                    document.Id,
                    DocumentProcessingStatuses.Processing),
                MemberUserId));

        Assert.Equal(
            TransitionDocumentProcessingStatusHandler.ConcurrentlyModifiedMessage,
            exception.Message);
        Assert.Equal(UpdateDocumentHandler.ConcurrentlyModifiedMessage, exception.Message);

        // The two 409 reasons this handler can produce must never read the same.
        Assert.NotEqual(
            TransitionDocumentProcessingStatusHandler.InvalidTransitionMessage,
            exception.Message);
    }

    /// <summary>
    /// The losing transition must leave the winner's committed status in place — no silent
    /// last-write-wins.
    /// </summary>
    /// <remarks>
    /// Two separate repositories stand in for the two DbContexts two concurrent requests get:
    /// <see cref="_repository"/> holds the document as it is actually stored, and the loser works
    /// from its own stale copy read before the winner committed. Mirrors the Slice 2.1 upload
    /// concurrency test, where the real PostgreSQL half of this is covered by an integration
    /// test.
    /// </remarks>
    [Fact]
    public async Task HandleAsync_WhenItLosesTheConcurrencyRace_ShouldNotOverwriteTheWinnersStatus()
    {
        // The winner already moved this document on and committed.
        var document = SeedDocument(DocumentProcessingStatuses.Uploaded);

        await CreateHandler().HandleAsync(
            new TransitionDocumentProcessingStatusCommand(
                document.Id,
                DocumentProcessingStatuses.Processing),
            MemberUserId);

        // The loser read the document before that, so it still believes the status is Uploaded
        // and its own Uploaded -> Processing move looks legal.
        var staleRepository = new FakeDocumentRepository
        {
            SaveChangesException = new ConcurrencyConflictException(
                "The document was modified by another request.",
                new InvalidOperationException("row version mismatch"))
        };

        staleRepository.Seed(new DocumentMetadata
        {
            Id = document.Id,
            ProjectId = document.ProjectId,
            Title = document.Title,
            CreatedByUserId = document.CreatedByUserId,
            StorageFileName = document.StorageFileName,
            Status = DocumentProcessingStatuses.Uploaded,
            CreatedAt = document.CreatedAt,
            UpdatedAt = document.UpdatedAt
        });

        var loser = new TransitionDocumentProcessingStatusHandler(
            staleRepository,
            _membership,
            new TransitionDocumentProcessingStatusValidator());

        await Assert.ThrowsAsync<ConflictException>(() =>
            loser.HandleAsync(
                new TransitionDocumentProcessingStatusCommand(
                    document.Id,
                    DocumentProcessingStatuses.Processing),
                MemberUserId));

        // The stored document is still the winner's, and the winner's single save is the only
        // one that reached storage.
        var stored = await _repository.GetByIdAsync(document.Id);
        Assert.Equal(DocumentProcessingStatuses.Processing, stored!.Status);
        Assert.Equal(1, _repository.SaveChangesCallCount);
    }

    /// <summary>
    /// Only a lost race becomes a 409 — a genuine persistence fault must stay an unexpected
    /// failure rather than something a caller could read as "just reload and retry".
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenPersistenceFailsForAnyOtherReason_ShouldNotBeReportedAsConflict()
    {
        var document = SeedDocument(DocumentProcessingStatuses.Uploaded);
        _repository.SaveChangesException = new InvalidOperationException("database unavailable");

        var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
            CreateHandler().HandleAsync(
                new TransitionDocumentProcessingStatusCommand(
                    document.Id,
                    DocumentProcessingStatuses.Processing),
                MemberUserId));

        Assert.IsNotType<ConflictException>(exception);
        Assert.IsType<InvalidOperationException>(exception);
    }
}
