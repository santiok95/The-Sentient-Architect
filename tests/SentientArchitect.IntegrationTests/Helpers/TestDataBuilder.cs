using SentientArchitect.Data;

namespace SentientArchitect.IntegrationTests.Helpers;

/// <summary>
/// Creates well-known test users in the database to satisfy FK constraints.
/// </summary>
public static class TestDataBuilder
{
    /// <summary>Inserts a minimal ApplicationUser and returns its Id.</summary>
    public static async Task<Guid> CreateUserAsync(ApplicationContext context, Guid? userId = null)
    {
        var id = userId ?? Guid.NewGuid();

        // Skip if already exists (test collection reuses the same container)
        if (await context.Users.FindAsync(id) is not null)
            return id;

        var user = new ApplicationUser
        {
            Id           = id,
            UserName     = $"testuser_{id:N}",
            Email        = $"test_{id:N}@test.com",
            NormalizedUserName = $"TESTUSER_{id:N}".ToUpperInvariant(),
            NormalizedEmail    = $"TEST_{id:N}@TEST.COM".ToUpperInvariant(),
            DisplayName  = "Test User",
            TenantId     = id, // user is own tenant for simplicity
            CreatedAt    = DateTime.UtcNow,
            SecurityStamp = Guid.NewGuid().ToString(),
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return id;
    }
}
