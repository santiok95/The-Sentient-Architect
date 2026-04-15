namespace SentientArchitect.Application.Features.Profile.UpdateProfile;

public record UpdateProfileResponse(
    Guid UserId,
    List<string> PreferredStack,
    List<string> KnownPatterns,
    string? InfrastructureContext,
    string? TeamSize,
    string? ExperienceLevel,
    string? CustomNotes,
    DateTime LastUpdatedAt);
