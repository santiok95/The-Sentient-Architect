using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SentientArchitect.Data.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class RenameFindingSeverityValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename FindingSeverity enum string values stored in the AnalysisFindings table.
            // Old: Info / Warning / Error / Critical
            // New: Low  / Medium  / High  / Critical
            migrationBuilder.Sql("""UPDATE "AnalysisFindings" SET "Severity" = 'Low'    WHERE "Severity" = 'Info';""");
            migrationBuilder.Sql("""UPDATE "AnalysisFindings" SET "Severity" = 'Medium' WHERE "Severity" = 'Warning';""");
            migrationBuilder.Sql("""UPDATE "AnalysisFindings" SET "Severity" = 'High'   WHERE "Severity" = 'Error';""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""UPDATE "AnalysisFindings" SET "Severity" = 'Info'    WHERE "Severity" = 'Low';""");
            migrationBuilder.Sql("""UPDATE "AnalysisFindings" SET "Severity" = 'Warning' WHERE "Severity" = 'Medium';""");
            migrationBuilder.Sql("""UPDATE "AnalysisFindings" SET "Severity" = 'Error'   WHERE "Severity" = 'High';""");
        }
    }
}
