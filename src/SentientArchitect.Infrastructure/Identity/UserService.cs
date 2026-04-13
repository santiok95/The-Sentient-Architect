using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Data;

namespace SentientArchitect.Infrastructure.Identity;

public class UserService(UserManager<ApplicationUser> userManager) : IUserService
{
    public async Task<Dictionary<Guid, UserSummary>> GetUserSummariesAsync(
        IEnumerable<Guid> userIds,
        CancellationToken ct = default)
    {
        var ids = userIds.ToList();
        if (ids.Count == 0)
            return [];

        var users = await userManager.Users
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .AsNoTracking()
            .ToListAsync(ct);

        var result = new Dictionary<Guid, UserSummary>();
        foreach (var u in users)
        {
            // Determine role — we load the most common case (User / Admin)
            // Using UserManager.GetRolesAsync per-user would be N+1, so we default to "User"
            // and override for known admins via a second query.
            result[u.Id] = new UserSummary(u.Id, u.DisplayName, "User");
        }

        // Batch role lookup: get all users in the Admin role that are in our set
        var adminUsers = await userManager.GetUsersInRoleAsync("Admin");
        foreach (var admin in adminUsers)
        {
            if (result.ContainsKey(admin.Id))
                result[admin.Id] = result[admin.Id] with { Role = "Admin" };
        }

        return result;
    }
}
