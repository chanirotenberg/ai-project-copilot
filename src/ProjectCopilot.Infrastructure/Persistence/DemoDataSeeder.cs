using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProjectCopilot.Domain.Entities;
using ProjectCopilot.Infrastructure.Identity;

namespace ProjectCopilot.Infrastructure.Persistence;

public sealed class DemoDataSeeder
{
    private static readonly Guid DemoProjectId =
        Guid.Parse("980f644c-039f-4031-80b0-c07ff3d14993");

    private static readonly Guid DemoUserId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    // Development-only QA credential, intentionally public (documented in README). Never
    // written to appsettings.json, any production config, or a secrets store - it exists
    // only as this literal (reachable solely via --seed-demo, which Program.cs refuses to
    // run outside Development) and in the README's documentation of it.
    private const string DemoUserEmail = "demo@projectcopilot.local";
    private const string DemoUserPassword = "DemoUser1234";

    private static readonly Guid ProjectAId = Guid.Parse("a0000000-0000-0000-0000-00000000000a");
    private static readonly Guid ProjectBId = Guid.Parse("a0000000-0000-0000-0000-00000000000b");
    private static readonly Guid ProjectCId = Guid.Parse("a0000000-0000-0000-0000-00000000000c");
    private static readonly Guid ProjectDId = Guid.Parse("a0000000-0000-0000-0000-00000000000d");

    private static readonly Guid TaskA1Id = Guid.Parse("b0000000-0000-0000-0000-0000000000a1");
    private static readonly Guid TaskA2Id = Guid.Parse("b0000000-0000-0000-0000-0000000000a2");
    private static readonly Guid TaskA3Id = Guid.Parse("b0000000-0000-0000-0000-0000000000a3");
    private static readonly Guid TaskA4Id = Guid.Parse("b0000000-0000-0000-0000-0000000000a4");
    private static readonly Guid TaskB1Id = Guid.Parse("b0000000-0000-0000-0000-0000000000b1");
    private static readonly Guid TaskB2Id = Guid.Parse("b0000000-0000-0000-0000-0000000000b2");
    private static readonly Guid TaskB3Id = Guid.Parse("b0000000-0000-0000-0000-0000000000b3");
    private static readonly Guid TaskC1Id = Guid.Parse("b0000000-0000-0000-0000-0000000000c1");
    private static readonly Guid TaskC2Id = Guid.Parse("b0000000-0000-0000-0000-0000000000c2");

    private static readonly Guid DocA1Id = Guid.Parse("c0000000-0000-0000-0000-0000000000a1");
    private static readonly Guid DocA2Id = Guid.Parse("c0000000-0000-0000-0000-0000000000a2");
    private static readonly Guid DocB1Id = Guid.Parse("c0000000-0000-0000-0000-0000000000b1");
    private static readonly Guid DocB2Id = Guid.Parse("c0000000-0000-0000-0000-0000000000b2");
    private static readonly Guid DocB3Id = Guid.Parse("c0000000-0000-0000-0000-0000000000b3");

    private readonly AppDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;

    public DemoDataSeeder(AppDbContext dbContext, UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
    }

    public async Task SeedAsync(
        CancellationToken cancellationToken = default)
    {
        // Creates/validates the real, loginable demo ApplicationUser first - every other
        // seeded row below references this id, and a collision here must stop the whole
        // seed rather than silently proceed against unexpected existing data.
        await EnsureDemoUserAsync(cancellationToken);

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

        // Independent idempotency guard: must fire even if the demo project already existed
        // from a prior seed run before this slice, otherwise the existing demo project becomes
        // inaccessible to the demo user under the new membership-based authorization.
        var membershipExists = await _dbContext.ProjectMembers
            .AnyAsync(
                member => member.ProjectId == DemoProjectId && member.UserId == DemoUserId,
                cancellationToken);

        if (!membershipExists)
        {
            await _dbContext.ProjectMembers.AddAsync(
                new ProjectMember
                {
                    ProjectId = DemoProjectId,
                    UserId = DemoUserId,
                    CreatedAtUtc = DateTime.UtcNow
                },
                cancellationToken);

            await _dbContext.SaveChangesAsync(
                cancellationToken);
        }

        await SeedAdditionalProjectsAsync(cancellationToken);
    }

    // Collision-safety ordering: check by fixed id first, then by email, so each collision
    // type gets a precise, distinct error. Never resets/overwrites an existing account's
    // password on any rerun - a match on id+email is treated as already correctly seeded.
    private async Task EnsureDemoUserAsync(CancellationToken cancellationToken)
    {
        var existingById = await _userManager.FindByIdAsync(DemoUserId.ToString());

        if (existingById is not null)
        {
            if (!string.Equals(existingById.Email, DemoUserEmail, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Demo user seed collision: an ApplicationUser with id '{DemoUserId}' already " +
                    $"exists with email '{existingById.Email}', expected '{DemoUserEmail}'. Refusing " +
                    "to modify this account.");
            }

            // Already correctly seeded from a prior run - do not touch/reset its password.
            return;
        }

        var existingByEmail = await _userManager.FindByEmailAsync(DemoUserEmail);

        if (existingByEmail is not null)
        {
            throw new InvalidOperationException(
                $"Demo user seed collision: an ApplicationUser with email '{DemoUserEmail}' already " +
                $"exists under a different id '{existingByEmail.Id}', expected '{DemoUserId}'. Refusing " +
                "to create a duplicate account.");
        }

        var user = new ApplicationUser
        {
            Id = DemoUserId,
            UserName = DemoUserEmail,
            Email = DemoUserEmail
        };

        var result = await _userManager.CreateAsync(user, DemoUserPassword);

        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));

            throw new InvalidOperationException(
                $"Failed to create the demo ApplicationUser ('{DemoUserId}'): {errors}");
        }
    }

    // Additive QA dataset: 4 new projects covering varied Task/Document shapes for manual
    // testing through the real UI (loading/error/empty states, overdue-but-active work,
    // blocked tasks, unassigned tasks, null descriptions, projects with/without documents).
    // Deliberately does not touch the pre-existing "Checkout Platform V2" seed above.
    private async Task SeedAdditionalProjectsAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var today = now.Date;

        await EnsureProjectAsync(
            new Project
            {
                Id = ProjectAId,
                Name = "Website Redesign",
                Description =
                    "Full redesign of the public marketing website, covering a new visual " +
                    "identity, a responsive layout overhaul, and a rebuilt checkout flow. " +
                    "Coordinated across design and frontend engineering, with a staged " +
                    "rollout planned ahead of the next quarter's launch.",
                Status = "Active",
                Deadline = now.AddDays(30),
                CreatedByUserId = DemoUserId,
                CreatedAt = now,
                UpdatedAt = now
            },
            cancellationToken);

        await EnsureProjectAsync(
            new Project
            {
                Id = ProjectBId,
                Name = "Mobile App Launch",
                Description =
                    "Launch of the first public release of the mobile app to the App Store " +
                    "and Play Store, including store listing content, crash triage, and " +
                    "post-launch localization follow-up.",
                Status = "Active",
                // Deliberately in the past - the project stays "Active" regardless of a
                // missed deadline; Status is never derived from Deadline.
                Deadline = now.AddDays(-14),
                CreatedByUserId = DemoUserId,
                CreatedAt = now,
                UpdatedAt = now
            },
            cancellationToken);

        await EnsureProjectAsync(
            new Project
            {
                Id = ProjectCId,
                Name = "Internal Automation",
                Description = null,
                Status = "Active",
                Deadline = today,
                CreatedByUserId = DemoUserId,
                CreatedAt = now,
                UpdatedAt = now
            },
            cancellationToken);

        await EnsureProjectAsync(
            new Project
            {
                Id = ProjectDId,
                Name = "Empty Project",
                Description = "A project with no tasks or documents yet.",
                Status = "Active",
                Deadline = null,
                CreatedByUserId = DemoUserId,
                CreatedAt = now,
                UpdatedAt = now
            },
            cancellationToken);

        await EnsureMembershipAsync(ProjectAId, DemoUserId, cancellationToken);
        await EnsureMembershipAsync(ProjectBId, DemoUserId, cancellationToken);
        await EnsureMembershipAsync(ProjectCId, DemoUserId, cancellationToken);
        await EnsureMembershipAsync(ProjectDId, DemoUserId, cancellationToken);

        // Project A tasks
        await EnsureTaskAsync(
            new TaskItem
            {
                Id = TaskA1Id,
                ProjectId = ProjectAId,
                Title = "Design homepage mockup",
                Description = "Initial mockup for stakeholder review.",
                Status = "Done",
                Priority = "Medium",
                AssignedUserId = DemoUserId,
                DueDate = now.AddDays(-10),
                IsBlocked = false,
                CreatedAt = now,
                UpdatedAt = now
            },
            cancellationToken);

        await EnsureTaskAsync(
            new TaskItem
            {
                Id = TaskA2Id,
                ProjectId = ProjectAId,
                Title = "Implement responsive nav",
                Description =
                    "Rebuild the primary site navigation to work across mobile, tablet, and " +
                    "desktop breakpoints. Needs to support the new mega-menu content model and " +
                    "keep keyboard navigation accessible.",
                Status = "InProgress",
                Priority = "High",
                AssignedUserId = DemoUserId,
                DueDate = today,
                IsBlocked = false,
                CreatedAt = now,
                UpdatedAt = now
            },
            cancellationToken);

        await EnsureTaskAsync(
            new TaskItem
            {
                Id = TaskA3Id,
                ProjectId = ProjectAId,
                Title = "Fix checkout button alignment",
                Description = null,
                Status = "Todo",
                Priority = "Low",
                AssignedUserId = null,
                DueDate = null,
                IsBlocked = false,
                CreatedAt = now,
                UpdatedAt = now
            },
            cancellationToken);

        await EnsureTaskAsync(
            new TaskItem
            {
                Id = TaskA4Id,
                ProjectId = ProjectAId,
                Title = "Waiting on design approval",
                Description = "Blocked pending sign-off from the design lead.",
                Status = "Todo",
                Priority = "Critical",
                AssignedUserId = DemoUserId,
                DueDate = now.AddDays(7),
                IsBlocked = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            cancellationToken);

        // Project A documents
        await EnsureDocumentAsync(
            new DocumentMetadata
            {
                Id = DocA1Id,
                ProjectId = ProjectAId,
                Title = "Project Brief",
                Description =
                    "Summarizes goals, scope, and success metrics for the website redesign, " +
                    "including the target audience, the key pages in scope, and the review " +
                    "process for design handoff.",
                CreatedByUserId = DemoUserId,
                CreatedAt = now,
                UpdatedAt = now
            },
            cancellationToken);

        await EnsureDocumentAsync(
            new DocumentMetadata
            {
                Id = DocA2Id,
                ProjectId = ProjectAId,
                Title = "Requirements",
                Description = null,
                CreatedByUserId = DemoUserId,
                CreatedAt = now,
                UpdatedAt = now
            },
            cancellationToken);

        // Project B tasks
        await EnsureTaskAsync(
            new TaskItem
            {
                Id = TaskB1Id,
                ProjectId = ProjectBId,
                Title = "Submit to App Store",
                Description = "Submitted build 1.0.0 for review.",
                Status = "Done",
                Priority = "High",
                AssignedUserId = DemoUserId,
                DueDate = now.AddDays(-20),
                IsBlocked = false,
                CreatedAt = now,
                UpdatedAt = now
            },
            cancellationToken);

        await EnsureTaskAsync(
            new TaskItem
            {
                Id = TaskB2Id,
                ProjectId = ProjectBId,
                Title = "Fix crash on launch screen",
                Description =
                    "App crashes on cold start for a subset of Android devices running " +
                    "older OS versions. Needs a fix and a hotfix release; currently the " +
                    "top-priority open issue for this project.",
                Status = "InProgress",
                Priority = "Critical",
                AssignedUserId = DemoUserId,
                DueDate = now.AddDays(-3),
                IsBlocked = false,
                CreatedAt = now,
                UpdatedAt = now
            },
            cancellationToken);

        await EnsureTaskAsync(
            new TaskItem
            {
                Id = TaskB3Id,
                ProjectId = ProjectBId,
                Title = "Localize UI strings",
                Description = "Translate remaining strings for launch locales.",
                Status = "Todo",
                Priority = "Medium",
                AssignedUserId = null,
                DueDate = null,
                IsBlocked = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            cancellationToken);

        // Project B documents
        await EnsureDocumentAsync(
            new DocumentMetadata
            {
                Id = DocB1Id,
                ProjectId = ProjectBId,
                Title = "Meeting Notes",
                Description = "Notes from the weekly launch sync.",
                CreatedByUserId = DemoUserId,
                CreatedAt = now,
                UpdatedAt = now
            },
            cancellationToken);

        await EnsureDocumentAsync(
            new DocumentMetadata
            {
                Id = DocB2Id,
                ProjectId = ProjectBId,
                Title = "Release Checklist",
                Description =
                    "Step-by-step checklist covering build signing, store metadata, " +
                    "screenshots, and the staged rollout plan for each app store release.",
                CreatedByUserId = DemoUserId,
                CreatedAt = now,
                UpdatedAt = now
            },
            cancellationToken);

        await EnsureDocumentAsync(
            new DocumentMetadata
            {
                Id = DocB3Id,
                ProjectId = ProjectBId,
                Title = "App Store Listing Copy",
                Description = null,
                CreatedByUserId = DemoUserId,
                CreatedAt = now,
                UpdatedAt = now
            },
            cancellationToken);

        // Project C tasks (no documents for this project, deliberately)
        await EnsureTaskAsync(
            new TaskItem
            {
                Id = TaskC1Id,
                ProjectId = ProjectCId,
                Title = "Write deployment script",
                Description = null,
                Status = "Todo",
                Priority = "Medium",
                AssignedUserId = null,
                DueDate = now.AddDays(10),
                IsBlocked = false,
                CreatedAt = now,
                UpdatedAt = now
            },
            cancellationToken);

        await EnsureTaskAsync(
            new TaskItem
            {
                Id = TaskC2Id,
                ProjectId = ProjectCId,
                Title = "Review automation PR",
                Description = "Review open pull request for the nightly job.",
                Status = "InProgress",
                Priority = "Low",
                AssignedUserId = DemoUserId,
                DueDate = null,
                IsBlocked = false,
                CreatedAt = now,
                UpdatedAt = now
            },
            cancellationToken);

        // Project D ("Empty Project") intentionally has no tasks and no documents.

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureProjectAsync(Project project, CancellationToken cancellationToken)
    {
        var exists = await _dbContext.Projects
            .AnyAsync(p => p.Id == project.Id, cancellationToken);

        if (!exists)
        {
            await _dbContext.Projects.AddAsync(project, cancellationToken);
        }
    }

    private async Task EnsureMembershipAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var exists = await _dbContext.ProjectMembers
            .AnyAsync(
                m => m.ProjectId == projectId && m.UserId == userId,
                cancellationToken);

        if (!exists)
        {
            await _dbContext.ProjectMembers.AddAsync(
                new ProjectMember
                {
                    ProjectId = projectId,
                    UserId = userId,
                    CreatedAtUtc = DateTime.UtcNow
                },
                cancellationToken);
        }
    }

    private async Task EnsureTaskAsync(TaskItem task, CancellationToken cancellationToken)
    {
        var exists = await _dbContext.Tasks
            .AnyAsync(t => t.Id == task.Id, cancellationToken);

        if (!exists)
        {
            await _dbContext.Tasks.AddAsync(task, cancellationToken);
        }
    }

    private async Task EnsureDocumentAsync(DocumentMetadata document, CancellationToken cancellationToken)
    {
        var exists = await _dbContext.Documents
            .AnyAsync(d => d.Id == document.Id, cancellationToken);

        if (!exists)
        {
            await _dbContext.Documents.AddAsync(document, cancellationToken);
        }
    }
}
