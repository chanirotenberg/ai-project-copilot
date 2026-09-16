using Microsoft.EntityFrameworkCore;
using ProjectCopilot.Domain.Entities;

namespace ProjectCopilot.Infrastructure.Persistence;

public sealed class DemoDataSeeder
{
    private static readonly Guid DemoProjectId =
        Guid.Parse("980f644c-039f-4031-80b0-c07ff3d14993");

    private static readonly Guid DemoUserId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly AppDbContext _dbContext;

    public DemoDataSeeder(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SeedAsync(
        CancellationToken cancellationToken = default)
    {
        var projectExists = await _dbContext.Projects
            .AnyAsync(
                project => project.Id == DemoProjectId,
                cancellationToken);

        if (!projectExists)
        {
            var now = DateTime.UtcNow;

            var project = new Project
            {
                Id = DemoProjectId,
                Name = "Checkout Platform V2",
                Description = "Demo project for AI Project Copilot",
                Status = "Active",
                Deadline = new DateTime(
                    2026,
                    10,
                    1,
                    0,
                    0,
                    0,
                    DateTimeKind.Utc),
                CreatedByUserId = DemoUserId,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _dbContext.Projects.AddAsync(
                project,
                cancellationToken);

            await _dbContext.SaveChangesAsync(
                cancellationToken);
        }
    }
}