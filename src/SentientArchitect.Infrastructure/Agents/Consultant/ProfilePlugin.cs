using System.ComponentModel;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using SentientArchitect.Application.Common.Interfaces;

namespace SentientArchitect.Infrastructure.Agents.Consultant;

public sealed class ProfilePlugin(IApplicationDbContext db)
{
    [KernelFunction, Description("Get the user's technical profile including preferred tech stack, known patterns, infrastructure context, team size, and experience level. Use this at the start of a consultation to personalize advice.")]
    public async Task<string> GetUserProfileAsync(
        [Description("User ID to fetch profile for")] string userId,
        CancellationToken cancellationToken = default)
    {
        var userGuid = Guid.Parse(userId);
        var profile  = await db.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userGuid, cancellationToken);

        if (profile is null)
            return "No profile found. Ask the user about their tech stack and experience to personalize your advice.";

        var summary = new
        {
            profile.ExperienceLevel,
            profile.TeamSize,
            profile.InfrastructureContext,
            PreferredStack = profile.PreferredStack,
            KnownPatterns  = profile.KnownPatterns,
            profile.CustomNotes
        };

        return JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
    }
}
