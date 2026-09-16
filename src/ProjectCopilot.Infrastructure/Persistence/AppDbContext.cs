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

    // Enforces email uniqueness at the database level. Without this, two concurrent
    // registrations with the same email could both pass the application-level
    // RequireUniqueEmail check before either commits.
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // required — IdentityUserContext's own Identity table config runs here

        builder.Entity<ApplicationUser>().HasIndex(u => u.NormalizedEmail).IsUnique();
    }
}
