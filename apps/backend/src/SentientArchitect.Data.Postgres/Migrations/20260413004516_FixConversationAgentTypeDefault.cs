using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SentientArchitect.Data.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class FixConversationAgentTypeDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill rows that got the empty-string default from the previous migration
            migrationBuilder.Sql("""
                UPDATE "Conversations" SET "AgentType" = 'Knowledge' WHERE "AgentType" = '';
                """);

            migrationBuilder.AlterColumn<string>(
                name: "AgentType",
                table: "Conversations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Knowledge",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "AgentType",
                table: "Conversations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValue: "Knowledge");
        }
    }
}
