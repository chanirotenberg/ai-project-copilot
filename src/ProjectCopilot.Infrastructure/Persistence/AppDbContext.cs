using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ProjectCopilot.Domain.Entities;
using ProjectCopilot.Infrastructure.Identity;

namespace ProjectCopilot.Infrastructure.Persistence;

public class AppDbContext : IdentityUserContext<ApplicationUser, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();

    // Enforces email uniqueness at the database level. Without this, two concurrent
    // registrations with the same email could both pass the application-level
    // RequireUniqueEmail check before either commits.
    //
    // Project name uniqueness (per-creator, case-insensitive, trimmed) is enforced by a
    // PostgreSQL expression unique index, not represented in this model — see migration
    // 20261007110426_AddProjectNameUniquePerCreator.
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // required — IdentityUserContext's own Identity table config runs here

        builder.Entity<ApplicationUser>().HasIndex(u => u.NormalizedEmail).IsUnique();

        builder.Entity<RefreshToken>(entity =>
        {
            entity.HasIndex(t => t.TokenHash).IsUnique();
            entity.HasIndex(t => t.UserId);

            // Maps RowVersion to PostgreSQL's real xmin system column (already present on every
            // table) instead of an app-managed column, so rotation's concurrency check is backed
            // by Postgres itself rather than a value this code has to maintain.
            entity.Property(t => t.RowVersion)
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsRowVersion();
        });

        builder.Entity<ProjectMember>(entity =>
        {
            entity.HasKey(m => new { m.ProjectId, m.UserId });
            entity.HasIndex(m => m.UserId);

            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(m => m.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
