using Microsoft.EntityFrameworkCore;
using ProjectCopilot.Application.Abstractions;
using ProjectCopilot.Application.Common.Exceptions;
using ProjectCopilot.Infrastructure.Persistence;

namespace ProjectCopilot.Infrastructure.Repositories;

public class ProjectMembershipService : IProjectMembershipService
{
    private readonly AppDbContext _dbContext;

    public ProjectMembershipService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task EnsureMemberAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var isMember = await _dbContext.ProjectMembers
            .AsNoTracking()
            .AnyAsync(
                member => member.ProjectId == projectId && member.UserId == userId,
                cancellationToken);

        if (!isMember)
        {
            throw new NotFoundException(
                $"Project with id '{projectId}' was not found.");
        }
    }
}
