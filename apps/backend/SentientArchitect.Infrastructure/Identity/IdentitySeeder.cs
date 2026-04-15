using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SentientArchitect.Data;

namespace SentientArchitect.Infrastructure.Identity;

/// <summary>
/// Seeds the two application roles (Admin / User) and an initial admin account on first run.
/// Fully idempotent — safe to call on every startup.
/// </summary>
public sealed class IdentitySeeder(
    RoleManager<IdentityRole<Guid>> roleManager,
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration,
    ILogger<IdentitySeeder> logger)
{
    private static readonly string[] RequiredRoles = ["Admin", "User"];

    public async Task SeedAsync()
    {
        await SeedRolesAsync();
        await SeedAdminUserAsync();
    }

    // ── Roles ────────────────────────────────────────────────────────────────

    private async Task SeedRolesAsync()
    {
        foreach (var roleName in RequiredRoles)
        {
            if (await roleManager.RoleExistsAsync(roleName))
                continue;

            var result = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));

            if (result.Succeeded)
                logger.LogInformation("Role '{Role}' created.", roleName);
            else
                logger.LogError("Failed to create role '{Role}': {Errors}",
                    roleName, string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }

    // ── Admin user ───────────────────────────────────────────────────────────

    private async Task SeedAdminUserAsync()
    {
        var email    = configuration["Seeder:AdminEmail"];
        var password = configuration["Seeder:AdminPassword"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Seeder:AdminEmail or Seeder:AdminPassword not configured — skipping admin seed.");
            return;
        }

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            logger.LogDebug("Admin user '{Email}' already exists — skipping.", email);
            return;
        }

        var adminUser = new ApplicationUser
        {
            Id          = Guid.NewGuid(),
            UserName    = email,
            Email       = email,
            DisplayName = "System Admin",
            TenantId    = Guid.Empty,          // global tenant
            CreatedAt   = DateTime.UtcNow,
            IsActive    = true,
            EmailConfirmed = true,
        };

        var createResult = await userManager.CreateAsync(adminUser, password);
        if (!createResult.Succeeded)
        {
            logger.LogError("Failed to create admin user: {Errors}",
                string.Join(", ", createResult.Errors.Select(e => e.Description)));
            return;
        }

        var roleResult = await userManager.AddToRoleAsync(adminUser, "Admin");
        if (roleResult.Succeeded)
            logger.LogInformation("Admin user '{Email}' created and assigned Admin role.", email);
        else
            logger.LogError("Admin user created but role assignment failed: {Errors}",
                string.Join(", ", roleResult.Errors.Select(e => e.Description)));
    }
}
