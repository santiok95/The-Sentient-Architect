using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SentientArchitect.Data.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddTrendCategoriesAndGitHubStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GitHubUrl",
                table: "TechnologyTrends",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StarCount",
                table: "TechnologyTrends",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GitHubUrl",
                table: "TechnologyTrends");

            migrationBuilder.DropColumn(
                name: "StarCount",
                table: "TechnologyTrends");
        }
    }
}
