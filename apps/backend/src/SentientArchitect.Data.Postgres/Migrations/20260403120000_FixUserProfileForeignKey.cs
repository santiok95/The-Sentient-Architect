using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SentientArchitect.Data.Postgres.Migrations;

/// <inheritdoc />
public partial class FixUserProfileForeignKey : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Drop the broken FK that points to the "User" table (domain POCO, not Identity)
        migrationBuilder.DropForeignKey(
            name: "FK_UserProfiles_User_UserId",
            table: "UserProfiles");

        // Drop the orphan "User" table — it was created by the initial migration
        // before builder.Ignore<User>() was in place. Identity uses "Users" table.
        migrationBuilder.DropTable(name: "User");

        // Create the correct FK pointing to the "Users" table (ApplicationUser / Identity)
        migrationBuilder.AddForeignKey(
            name: "FK_UserProfiles_Users_UserId",
            table: "UserProfiles",
            column: "UserId",
            principalTable: "Users",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_UserProfiles_Users_UserId",
            table: "UserProfiles");

        migrationBuilder.CreateTable(
            name: "User",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Email = table.Column<string>(type: "text", nullable: false),
                UserName = table.Column<string>(type: "text", nullable: false),
                DisplayName = table.Column<string>(type: "text", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_User", x => x.Id);
            });

        migrationBuilder.AddForeignKey(
            name: "FK_UserProfiles_User_UserId",
            table: "UserProfiles",
            column: "UserId",
            principalTable: "User",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }
}
