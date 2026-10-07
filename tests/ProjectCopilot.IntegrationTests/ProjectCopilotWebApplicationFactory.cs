using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using ProjectCopilot.Infrastructure.Persistence;

namespace ProjectCopilot.IntegrationTests;

// Test database isolation: each test run (process) gets its own freshly created, freshly
// migrated PostgreSQL database, named with a timestamp + guid so concurrent/sibling test runs
// never collide and never touch the real `projectcopilot` dev database. The database is
// dropped on normal process exit; a crashed/killed run leaves it behind for the next run's
// ReapStaleTestDatabasesAsync to clean up.
public class ProjectCopilotWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestJwtKey = "integration-tests-only-jwt-signing-key-32bytes-min";
    public const string TestJwtIssuer = "Test";
    public const string TestJwtAudience = "Test";

    private const string AdminConnectionString = "Host=localhost;Port=5433;Username=postgres;Password=postgres";
    private static readonly long RunEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    private static readonly string TestDbName = $"projectcopilot_test_{RunEpoch}_{Guid.NewGuid():N}";
    private static readonly string TestConnectionString =
        $"Host=localhost;Port=5433;Database={TestDbName};Username=postgres;Password=postgres";

    private static readonly Lazy<Task> InitializeOnce =
        new(InitializeAsync, LazyThreadSafetyMode.ExecutionAndPublication);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        InitializeOnce.Value.GetAwaiter().GetResult();

        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = TestConnectionString,
                ["Jwt:Key"] = TestJwtKey,
                ["Jwt:Issuer"] = TestJwtIssuer,
                ["Jwt:Audience"] = TestJwtAudience,
                ["Jwt:AccessTokenExpiryMinutes"] = "60"
            });
        });
    }

    private static async Task InitializeAsync()
    {
        await using var admin = new NpgsqlConnection(AdminConnectionString);
        await admin.OpenAsync();

        await ReapStaleTestDatabasesAsync(admin);

        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{TestDbName}\";", admin))
        {
            await create.ExecuteNonQueryAsync();
        }

        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(TestConnectionString).Options;
        await using var context = new AppDbContext(options);
        await context.Database.MigrateAsync();

        AppDomain.CurrentDomain.ProcessExit += (_, _) => DropDatabaseBestEffort();
    }

    // Reap databases from crashed/killed prior runs. Age-gated (1 hour) so a currently-running
    // sibling process's test DB (whose name necessarily has a fresh, recent epoch) is never
    // touched - only genuinely stale, orphaned databases are dropped.
    private static async Task ReapStaleTestDatabasesAsync(NpgsqlConnection admin)
    {
        const long staleThresholdSeconds = 3600;
        var cutoff = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - staleThresholdSeconds;
        var stale = new List<string>();

        await using (var list = new NpgsqlCommand(
            "SELECT datname FROM pg_database WHERE datname LIKE 'projectcopilot_test_%';", admin))
        await using (var reader = await list.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var name = reader.GetString(0);
                // Name format contract: "projectcopilot_test_<unix-epoch-seconds>_<guid-no-dashes>"
                // (see TestDbName above). parts[2] after Split('_') is the epoch. If that naming
                // scheme ever changes, update both TestDbName's construction and this parsing
                // together.
                var parts = name.Split('_');
                if (parts.Length >= 3 && long.TryParse(parts[2], out var epoch) && epoch < cutoff)
                {
                    stale.Add(name);
                }
            }
        }

        foreach (var name in stale)
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE);", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static void DropDatabaseBestEffort()
    {
        try
        {
            using var admin = new NpgsqlConnection(AdminConnectionString);
            admin.Open();
            using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{TestDbName}\" WITH (FORCE);", admin);
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // Best-effort only - a hard process kill/crash may skip this. The next run's
            // ReapStaleTestDatabasesAsync will eventually clean it up (after the 1-hour threshold).
        }
    }
}
