using ProjectCopilot.Domain.Entities;

namespace ProjectCopilot.Application.Abstractions;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Project>> GetAllForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Project project,
        CancellationToken cancellationToken = default);

    Task AddMemberAsync(
        ProjectMember member,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);
}