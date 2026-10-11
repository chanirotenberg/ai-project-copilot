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
            // Deliberately generic: this fires for "project doesn't exist" and "project exists
            // but caller isn't a member" alike, and must never embed the real projectId — doing
            // so would let a caller who derived this id from an unrelated resource (e.g. a
            // task/document they don't own) learn a foreign project's real GUID via the 404 body.
            throw NotFoundException.HiddenResource();
        }
    }
}
