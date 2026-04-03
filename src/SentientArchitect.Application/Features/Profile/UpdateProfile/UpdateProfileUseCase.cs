using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;
using SentientArchitect.Domain.Entities;

namespace SentientArchitect.Application.Features.Profile.UpdateProfile;

public class UpdateProfileUseCase(IApplicationDbContext db)
{
    public async Task<Result<UpdateProfileResponse>> ExecuteAsync(
        UpdateProfileRequest request,
        CancellationToken ct = default)
    {
        var profile = await db.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == request.UserId, ct);

        bool isNew = profile is null;

        if (isNew)
        {
            profile = new UserProfile(request.UserId);
            db.UserProfiles.Add(profile);
        }

        if (request.PreferredStack is not null)
            profile!.UpdatePreferredStack(request.PreferredStack);

        if (request.KnownPatterns is not null)
            profile!.UpdateKnownPatterns(request.KnownPatterns);

        if (request.InfrastructureContext is not null)
            profile!.UpdateInfrastructureContext(request.InfrastructureContext);

        if (request.TeamSize is not null)
            profile!.UpdateTeamSize(request.TeamSize);

        if (request.ExperienceLevel is not null)
            profile!.UpdateExperienceLevel(request.ExperienceLevel);

        if (request.CustomNotes is not null)
            profile!.UpdateCustomNotes(request.CustomNotes);

        await db.SaveChangesAsync(ct);

        var response = new UpdateProfileResponse(
            profile.UserId,
            profile.PreferredStack,
            profile.KnownPatterns,
            profile.InfrastructureContext,
            profile.TeamSize,
            profile.ExperienceLevel,
            profile.CustomNotes,
            profile.LastUpdatedAt);

        return Result<UpdateProfileResponse>.SuccessWith(response);
    }
}
