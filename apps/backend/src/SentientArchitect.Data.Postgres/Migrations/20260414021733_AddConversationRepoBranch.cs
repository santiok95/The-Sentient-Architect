using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SentientArchitect.Data.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationRepoBranch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActiveRepositoryBranch",
                table: "Conversations",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActiveRepositoryBranch",
                table: "Conversations");
        }
    }
}
