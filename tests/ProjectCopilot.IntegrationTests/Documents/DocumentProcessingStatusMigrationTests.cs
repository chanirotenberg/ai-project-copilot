using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using ProjectCopilot.Application.Documents;
using ProjectCopilot.Infrastructure.Persistence;

namespace ProjectCopilot.IntegrationTests.Documents;

/// <summary>
/// Proves the AddDocumentProcessingStatus migration behaves on a database that already holds
/// documents, which no other test can show: the shared integration database is created empty and
/// migrated in one go, so its backfill statement never has a row to touch.
/// </summary>
/// <remarks>
/// Runs against its own throwaway PostgreSQL database, created and dropped by the test. Its name
/// follows the same <c>projectcopilot_test_&lt;epoch&gt;_&lt;guid&gt;</c> convention as
/// <see cref="ProjectCopilotWebApplicationFactory"/> so that a database left behind by a killed
/// run is reaped by the next one.
/// </remarks>
public class DocumentProcessingStatusMigrationTests
{
    private const string AdminConnectionString =
        "Host=localhost;Port=5433;Username=postgres;Password=postgres";

    /// <summary>
    /// The migration immediately before AddDocumentProcessingStatus — the state of a database
    /// that has not yet seen the processing status column.
    /// </summary>
    private const string MigrationBeforeStatus = "20261013090232_AddDocumentConcurrencyToken";

    [Fact]
    public async Task Migration_OnADatabaseWithExistingDocuments_ShouldBackfillOnlyThoseWithAFile()
    {
        var databaseName =
            $"projectcopilot_test_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}_{Guid.NewGuid():N}";
        var connectionString =
            $"Host=localhost;Port=5433;Database={databaseName};Username=postgres;Password=postgres";

        await CreateDatabaseAsync(databaseName);

        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connectionString)
                .Options;

            // 1. Bring the database up to the state it had before this slice.
            await using (var context = new AppDbContext(options))
            {
                await context.GetService<IMigrator>().MigrateAsync(MigrationBeforeStatus);
            }

            Assert.False(
                await StatusColumnExistsAsync(connectionString),
                "The Status column must not exist before its migration runs.");

            // 2. Seed rows the way the old schema allowed: one document with a file, one without.
            var projectId = Guid.NewGuid();
            var withFileId = Guid.NewGuid();
            var withoutFileId = Guid.NewGuid();

            await InsertLegacyProjectAsync(connectionString, projectId);
            await InsertLegacyDocumentAsync(
                connectionString,
                withFileId,
                projectId,
                storageFileName: $"{Guid.NewGuid():N}.pdf");
            await InsertLegacyDocumentAsync(
                connectionString,
                withoutFileId,
                projectId,
                storageFileName: null);

            // 3. Apply this slice's migration over that existing data.
            await using (var context = new AppDbContext(options))
            {
                await context.Database.MigrateAsync();
            }

            Assert.True(
                await StatusColumnExistsAsync(connectionString),
                "The migration must add the Status column.");

            Assert.True(
                await StatusColumnIsNullableAsync(connectionString),
                "The Status column must be nullable: a document with no file has no status.");

            // 4. A document that already had a file has been processed by nobody, which is
            //    exactly what Uploaded means. A document with no file keeps a null status.
            Assert.Equal(
                DocumentProcessingStatuses.Uploaded,
                await ReadStatusAsync(connectionString, withFileId));

            Assert.Null(await ReadStatusAsync(connectionString, withoutFileId));

            // 5. The upgraded rows are usable by the running application's model, not just by
            //    raw SQL — including the state machine's entry point.
            await using (var context = new AppDbContext(options))
            {
                var upgraded = await context.Documents.SingleAsync(d => d.Id == withFileId);

                Assert.Equal(DocumentProcessingStatuses.Uploaded, upgraded.Status);
            }
        }
        finally
        {
            await DropDatabaseAsync(databaseName, connectionString);
        }
    }

    // ---------------------------------------------------------------- database helpers

    private static async Task CreateDatabaseAsync(string databaseName)
    {
        await using var admin = new NpgsqlConnection(AdminConnectionString);
        await admin.OpenAsync();

        await using var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\";", admin);
        await create.ExecuteNonQueryAsync();
    }

    private static async Task DropDatabaseAsync(string databaseName, string connectionString)
    {
        try
        {
            // Only this database's own pool: other tests in this process are pooling connections
            // to the shared integration database and must not be disturbed.
            using (var pooled = new NpgsqlConnection(connectionString))
            {
                NpgsqlConnection.ClearPool(pooled);
            }

            await using var admin = new NpgsqlConnection(AdminConnectionString);
            await admin.OpenAsync();

            await using var drop = new NpgsqlCommand(
                $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE);",
                admin);

            await drop.ExecuteNonQueryAsync();
        }
        catch
        {
            // Best-effort, exactly as in ProjectCopilotWebApplicationFactory: a leftover database
            // under the test naming convention is reaped by a later run and must never fail the
            // test it belongs to.
        }
    }

    private static async Task InsertLegacyProjectAsync(string connectionString, Guid projectId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO "Projects"
                ("Id", "Name", "Description", "Status", "Deadline", "CreatedByUserId", "CreatedAt", "UpdatedAt")
            VALUES
                (@id, @name, NULL, 'Active', NULL, @createdBy, @now, @now);
            """,
            connection);

        command.Parameters.AddWithValue("id", projectId);
        command.Parameters.AddWithValue("name", $"Legacy project {projectId:N}");
        command.Parameters.AddWithValue("createdBy", Guid.NewGuid());
        command.Parameters.AddWithValue("now", DateTime.UtcNow);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertLegacyDocumentAsync(
        string connectionString,
        Guid documentId,
        Guid projectId,
        string? storageFileName)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO "Documents"
                ("Id", "ProjectId", "Title", "Description", "CreatedByUserId",
                 "OriginalFileName", "ContentType", "SizeBytes", "StorageFileName",
                 "CreatedAt", "UpdatedAt")
            VALUES
                (@id, @projectId, @title, NULL, @createdBy,
                 @originalFileName, @contentType, @sizeBytes, @storageFileName,
                 @now, @now);
            """,
            connection);

        command.Parameters.AddWithValue("id", documentId);
        command.Parameters.AddWithValue("projectId", projectId);
        command.Parameters.AddWithValue("title", $"Legacy document {documentId:N}");
        command.Parameters.AddWithValue("createdBy", Guid.NewGuid());
        command.Parameters.AddWithValue(
            "originalFileName",
            storageFileName is null ? DBNull.Value : "legacy.pdf");
        command.Parameters.AddWithValue(
            "contentType",
            storageFileName is null ? DBNull.Value : "application/pdf");
        command.Parameters.AddWithValue(
            "sizeBytes",
            storageFileName is null ? DBNull.Value : 1024L);
        command.Parameters.AddWithValue(
            "storageFileName",
            storageFileName is null ? DBNull.Value : storageFileName);
        command.Parameters.AddWithValue("now", DateTime.UtcNow);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string?> ReadStatusAsync(string connectionString, Guid documentId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """SELECT "Status" FROM "Documents" WHERE "Id" = @id;""",
            connection);

        command.Parameters.AddWithValue("id", documentId);

        var value = await command.ExecuteScalarAsync();

        return value is DBNull or null ? null : (string)value;
    }

    private static async Task<bool> StatusColumnExistsAsync(string connectionString) =>
        await StatusColumnScalarAsync(connectionString, "SELECT COUNT(*)") > 0;

    private static async Task<bool> StatusColumnIsNullableAsync(string connectionString) =>
        await StatusColumnScalarAsync(
            connectionString,
            "SELECT COUNT(*) FILTER (WHERE is_nullable = 'YES')") > 0;

    private static async Task<long> StatusColumnScalarAsync(string connectionString, string selection)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            $"""
            {selection}
            FROM information_schema.columns
            WHERE table_name = 'Documents' AND column_name = 'Status';
            """,
            connection);

        return (long)(await command.ExecuteScalarAsync())!;
    }
}
