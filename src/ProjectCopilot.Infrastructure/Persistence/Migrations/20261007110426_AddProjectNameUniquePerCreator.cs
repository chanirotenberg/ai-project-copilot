using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectCopilot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    // Deliberately implemented as a raw-SQL PostgreSQL expression index (not via HasIndex) because
    // EF Core's fluent index API cannot express an expression over UPPER(BTRIM(...)) without a
    // shadow/computed column, which was explicitly ruled out to avoid dual normalization-semantics
    // engines (C# vs SQL) ever disagreeing. See AppDbContext.OnModelCreating for the model-side note.
    // Index name here must stay in sync with ProjectRepository.DuplicateProjectNameConstraintName
    // if ever renamed.
    public partial class AddProjectNameUniquePerCreator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX "IX_Projects_CreatedByUserId_NormalizedName"
                ON "Projects" ("CreatedByUserId", UPPER(BTRIM("Name")));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX "IX_Projects_CreatedByUserId_NormalizedName";
                """);
        }
    }
}
