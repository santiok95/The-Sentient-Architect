using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SentientArchitect.Data.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationConsultantContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ActiveRepositoryId",
                table: "Conversations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContextMode",
                table: "Conversations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PreferredStack",
                table: "Conversations",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_ActiveRepositoryId",
                table: "Conversations",
                column: "ActiveRepositoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_Repositories_ActiveRepositoryId",
                table: "Conversations",
                column: "ActiveRepositoryId",
                principalTable: "Repositories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Repositories_ActiveRepositoryId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_ActiveRepositoryId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "ActiveRepositoryId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "ContextMode",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "PreferredStack",
                table: "Conversations");
        }
    }
}
