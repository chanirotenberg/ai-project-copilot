using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectCopilot.Infrastructure.Identity;
using ProjectCopilot.Infrastructure.Persistence;

namespace ProjectCopilot.IntegrationTests;

// Exercises DemoDataSeeder directly against this test run's shared database (see
// ProjectCopilotWebApplicationFactory - its TestDbName/InitializeOnce are process-static, so
// ALL test classes in this assembly share one physical database, not one per class). Because
// of that, assertions here deliberately scope every query to the seeder's own fixed, known
// ids rather than whole-table counts - other test classes (e.g. ProjectMembershipAuthorizationTests)
// concurrently create their own Projects/Tasks/etc. with random ids in the same tables.
//
// Calls SeedAsync() directly rather than going through Program.cs's "--seed-demo" CLI path,
// since the WebApplicationFactory always runs under the "Testing" environment (not
// "Development"), which Program.cs's new IsDevelopment() guard would otherwise refuse. The
// guard itself is a one-line check in top-level statements in Program.cs with no seam to
// unit-test without adding new test infrastructure disproportionate to this slice; it was
// verified manually instead (see the slice's verification report).
public class DemoDataSeederTests : IClassFixture<ProjectCopilotWebApplicationFactory>
{
    private static readonly Guid DemoUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly Guid[] KnownProjectIds =
    [
        Guid.Parse("980f644c-039f-4031-80b0-c07ff3d14993"),
        Guid.Parse("a0000000-0000-0000-0000-00000000000a"),
        Guid.Parse("a0000000-0000-0000-0000-00000000000b"),
        Guid.Parse("a0000000-0000-0000-0000-00000000000c"),
        Guid.Parse("a0000000-0000-0000-0000-00000000000d")
    ];

    private static readonly Guid[] KnownTaskIds =
    [
        Guid.Parse("b0000000-0000-0000-0000-0000000000a1"),
        Guid.Parse("b0000000-0000-0000-0000-0000000000a2"),
        Guid.Parse("b0000000-0000-0000-0000-0000000000a3"),
        Guid.Parse("b0000000-0000-0000-0000-0000000000a4"),
        Guid.Parse("b0000000-0000-0000-0000-0000000000b1"),
        Guid.Parse("b0000000-0000-0000-0000-0000000000b2"),
        Guid.Parse("b0000000-0000-0000-0000-0000000000b3"),
        Guid.Parse("b0000000-0000-0000-0000-0000000000c1"),
        Guid.Parse("b0000000-0000-0000-0000-0000000000c2")
    ];

    private static readonly Guid[] KnownDocumentIds =
    [
        Guid.Parse("c0000000-0000-0000-0000-0000000000a1"),
        Guid.Parse("c0000000-0000-0000-0000-0000000000a2"),
        Guid.Parse("c0000000-0000-0000-0000-0000000000b1"),
        Guid.Parse("c0000000-0000-0000-0000-0000000000b2"),
        Guid.Parse("c0000000-0000-0000-0000-0000000000b3")
    ];

    private readonly ProjectCopilotWebApplicationFactory _factory;

    public DemoDataSeederTests(ProjectCopilotWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DemoDataSeeder>();
        await seeder.SeedAsync();
    }

    [Fact]
    public async Task SeedAsync_RunTwice_ProducesNoDuplicatesAmongKnownSeedIds()
    {
        await SeedAsync();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var projectCountAfterFirst = await dbContext.Projects
            .CountAsync(p => KnownProjectIds.Contains(p.Id));
        var taskCountAfterFirst = await dbContext.Tasks
            .CountAsync(t => KnownTaskIds.Contains(t.Id));
        var documentCountAfterFirst = await dbContext.Documents
            .CountAsync(d => KnownDocumentIds.Contains(d.Id));
        var memberCountAfterFirst = await dbContext.ProjectMembers
            .CountAsync(m => m.UserId == DemoUserId && KnownProjectIds.Contains(m.ProjectId));
        var userExistsAfterFirst = await userManager.FindByIdAsync(DemoUserId.ToString()) is not null;

        // Sanity check on the expected shape before asserting rerun-stability, so a future
        // change to the dataset's size fails loudly here instead of silently passing below.
        Assert.Equal(5, projectCountAfterFirst);
        Assert.Equal(9, taskCountAfterFirst);
        Assert.Equal(5, documentCountAfterFirst);
        Assert.Equal(5, memberCountAfterFirst);
        Assert.True(userExistsAfterFirst);

        await SeedAsync();

        var projectCountAfterSecond = await dbContext.Projects
            .CountAsync(p => KnownProjectIds.Contains(p.Id));
        var taskCountAfterSecond = await dbContext.Tasks
            .CountAsync(t => KnownTaskIds.Contains(t.Id));
        var documentCountAfterSecond = await dbContext.Documents
            .CountAsync(d => KnownDocumentIds.Contains(d.Id));
        var memberCountAfterSecond = await dbContext.ProjectMembers
            .CountAsync(m => m.UserId == DemoUserId && KnownProjectIds.Contains(m.ProjectId));
        var userExistsAfterSecond = await userManager.FindByIdAsync(DemoUserId.ToString()) is not null;

        Assert.Equal(projectCountAfterFirst, projectCountAfterSecond);
        Assert.Equal(taskCountAfterFirst, taskCountAfterSecond);
        Assert.Equal(documentCountAfterFirst, documentCountAfterSecond);
        Assert.Equal(memberCountAfterFirst, memberCountAfterSecond);
        Assert.Equal(userExistsAfterFirst, userExistsAfterSecond);
    }

    [Fact]
    public async Task SeedAsync_EverySeededAssignedTask_AssigneeHasMembershipOnThatProject()
    {
        await SeedAsync();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var seededTasks = await dbContext.Tasks
            .Where(t => KnownTaskIds.Contains(t.Id))
            .ToListAsync();

        var seededMemberships = await dbContext.ProjectMembers
            .Where(m => KnownProjectIds.Contains(m.ProjectId))
            .ToListAsync();

        var assignedTasks = seededTasks.Where(t => t.AssignedUserId is not null).ToList();
        Assert.NotEmpty(assignedTasks);

        foreach (var task in assignedTasks)
        {
            var hasMembership = seededMemberships.Any(
                m => m.ProjectId == task.ProjectId && m.UserId == task.AssignedUserId);

            Assert.True(
                hasMembership,
                $"Task '{task.Id}' is assigned to '{task.AssignedUserId}' who has no " +
                $"ProjectMember row for project '{task.ProjectId}'.");
        }
    }

    [Fact]
    public async Task SeedAsync_SeededRows_HaveNoOrphanedForeignKeys()
    {
        await SeedAsync();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existingProjectIds = (await dbContext.Projects
            .Where(p => KnownProjectIds.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync())
            .ToHashSet();

        Assert.Equal(5, existingProjectIds.Count);

        var seededTasks = await dbContext.Tasks
            .Where(t => KnownTaskIds.Contains(t.Id))
            .ToListAsync();
        var seededDocuments = await dbContext.Documents
            .Where(d => KnownDocumentIds.Contains(d.Id))
            .ToListAsync();
        var seededMemberships = await dbContext.ProjectMembers
            .Where(m => KnownProjectIds.Contains(m.ProjectId) && m.UserId == DemoUserId)
            .ToListAsync();

        Assert.All(seededTasks, t => Assert.Contains(t.ProjectId, existingProjectIds));
        Assert.All(seededDocuments, d => Assert.Contains(d.ProjectId, existingProjectIds));
        Assert.All(seededMemberships, m => Assert.Contains(m.ProjectId, existingProjectIds));
    }
}
