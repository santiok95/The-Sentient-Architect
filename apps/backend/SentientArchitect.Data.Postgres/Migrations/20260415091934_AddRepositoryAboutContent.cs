using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SentientArchitect.Data.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddRepositoryAboutContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AboutContent",
                table: "Repositories",
                type: "character varying(10000)",
                maxLength: 10000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AboutContent",
                table: "Repositories");
        }
    }
}
