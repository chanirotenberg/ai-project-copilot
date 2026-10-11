using ProjectCopilot.Domain.Entities;

namespace ProjectCopilot.Application.Abstractions;

public interface IDocumentRepository
{
    Task<DocumentMetadata?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentMetadata>> GetByProjectIdAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        DocumentMetadata document,
        CancellationToken cancellationToken = default);

    void Delete(DocumentMetadata document);

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);
}
