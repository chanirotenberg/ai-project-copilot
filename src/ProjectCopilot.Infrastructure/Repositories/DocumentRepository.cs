using Microsoft.EntityFrameworkCore;
using ProjectCopilot.Application.Abstractions;
using ProjectCopilot.Application.Common.Exceptions;
using ProjectCopilot.Domain.Entities;
using ProjectCopilot.Infrastructure.Persistence;

namespace ProjectCopilot.Infrastructure.Repositories;

public class DocumentRepository : IDocumentRepository
{
    private readonly AppDbContext _dbContext;

    public DocumentRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DocumentMetadata?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Documents
            .FirstOrDefaultAsync(
                document => document.Id == id,
                cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentMetadata>> GetByProjectIdAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Documents
            .AsNoTracking()
            .Where(document => document.ProjectId == projectId)
            .OrderByDescending(document => document.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        DocumentMetadata document,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Documents.AddAsync(document, cancellationToken);
    }

    public void Delete(DocumentMetadata document)
    {
        _dbContext.Documents.Remove(document);
    }

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            // A concurrent write already moved this document forward (its RowVersion/xmin no
            // longer matches what we read). Application must not reference EF Core, so the
            // translation happens here, at the only layer allowed to know about
            // DbUpdateConcurrencyException - same approach as RefreshTokenRepository.
            //
            // What the lost race *means* is caller-specific, so this stays neutral: the upload
            // handler turns it into a 409, and anything that does not handle it is reported as
            // an unexpected failure, exactly as a raw DbUpdateConcurrencyException was before.
            throw new ConcurrencyConflictException(
                "The document was modified by another request.",
                exception);
        }
    }
}
