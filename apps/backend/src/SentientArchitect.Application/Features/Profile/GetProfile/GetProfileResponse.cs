namespace SentientArchitect.Application.Features.Profile.GetProfile;

public record GetProfileResponse(
    Guid UserId,
    List<string> PreferredStack,
    List<string> KnownPatterns,
    string? InfrastructureContext,
    string? TeamSize,
    string? ExperienceLevel,
    string? CustomNotes,
    DateTime LastUpdatedAt);
