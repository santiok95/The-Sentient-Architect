using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;

namespace SentientArchitect.Application.Features.Profile.GetProfile;

public class GetProfileUseCase(IApplicationDbContext db)
{
    public async Task<Result<GetProfileResponse>> ExecuteAsync(
        GetProfileRequest request,
        CancellationToken ct = default)
    {
        var profile = await db.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == request.UserId, ct);

        if (profile is null)
        {
            return Result<GetProfileResponse>.SuccessWith(new GetProfileResponse(
                UserId: request.UserId,
                PreferredStack: [],
                KnownPatterns: [],
                InfrastructureContext: null,
                TeamSize: null,
                ExperienceLevel: null,
                CustomNotes: null,
                LastUpdatedAt: DateTime.UtcNow));
        }

        return Result<GetProfileResponse>.SuccessWith(new GetProfileResponse(
            UserId: profile.UserId,
            PreferredStack: profile.PreferredStack,
            KnownPatterns: profile.KnownPatterns,
            InfrastructureContext: profile.InfrastructureContext,
            TeamSize: profile.TeamSize,
            ExperienceLevel: profile.ExperienceLevel,
            CustomNotes: profile.CustomNotes,
            LastUpdatedAt: profile.LastUpdatedAt));
    }
}
