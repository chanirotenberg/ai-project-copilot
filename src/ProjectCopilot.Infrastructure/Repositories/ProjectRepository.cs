using Microsoft.EntityFrameworkCore;
using ProjectCopilot.Application.Abstractions;
using ProjectCopilot.Domain.Entities;
using ProjectCopilot.Infrastructure.Persistence;

namespace ProjectCopilot.Infrastructure.Repositories;

public class ProjectRepository : IProjectRepository
{
    private readonly AppDbContext _dbContext;

    public ProjectRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Project?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Projects
            .FirstOrDefaultAsync(
                project => project.Id == id,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Project>> GetAllForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Projects
            .AsNoTracking()
            .Where(project => _dbContext.ProjectMembers
                .Any(member => member.ProjectId == project.Id && member.UserId == userId))
            .OrderByDescending(project => project.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        Project project,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Projects.AddAsync(project, cancellationToken);
    }

    public async Task AddMemberAsync(
        ProjectMember member,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.ProjectMembers.AddAsync(member, cancellationToken);
    }

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}