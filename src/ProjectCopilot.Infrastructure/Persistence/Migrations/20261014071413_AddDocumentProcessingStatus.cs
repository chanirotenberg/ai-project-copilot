using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectCopilot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentProcessingStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Documents",
                type: "text",
                nullable: true);

            // Documents uploaded before this column existed already have a file and have never
            // been processed, which is exactly what "Uploaded" means — so they are backfilled
            // rather than left in a null state the state machine cannot move out of. Documents
            // with no file keep a null status: for them, the absence of a processing status is
            // the correct value.
            //
            // The "Status" IS NULL guard keeps the statement safe to re-run and makes it a no-op
            // on a database where the column was just created empty.
            migrationBuilder.Sql(
                """
                UPDATE "Documents"
                SET "Status" = 'Uploaded'
                WHERE "StorageFileName" IS NOT NULL AND "Status" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dropping the column is the exact inverse of adding it. The backfill is not undone
            // separately — the values it wrote live in this column and go with it.
            migrationBuilder.DropColumn(
                name: "Status",
                table: "Documents");
        }
    }
}
