using Microsoft.EntityFrameworkCore;
using ProjectCopilot.Application.Abstractions;
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
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
