using SentientArchitect.Domain.Abstractions;

namespace SentientArchitect.Domain.Entities;

public class UserProfile : BaseEntity
{
    public UserProfile(Guid userId)
    {
        UserId = userId;
        LastUpdatedAt = DateTime.UtcNow;
    }

    private UserProfile() { }

    public Guid UserId { get; private set; }
    public List<string> PreferredStack { get; private set; } = [];
    public List<string> KnownPatterns { get; private set; } = [];
    public string? InfrastructureContext { get; private set; }
    public string? TeamSize { get; private set; }
    public string? ExperienceLevel { get; private set; }
    public string? CustomNotes { get; private set; }
    public DateTime LastUpdatedAt { get; private set; }

    public User? User { get; private set; }

    public void UpdatePreferredStack(List<string> stack)
    {
        PreferredStack = stack;
        LastUpdatedAt = DateTime.UtcNow;
    }

    public void UpdateKnownPatterns(List<string> patterns)
    {
        KnownPatterns = patterns;
        LastUpdatedAt = DateTime.UtcNow;
    }

    public void UpdateInfrastructureContext(string? context)
    {
        InfrastructureContext = context;
        LastUpdatedAt = DateTime.UtcNow;
    }

    public void UpdateTeamSize(string? size)
    {
        TeamSize = size;
        LastUpdatedAt = DateTime.UtcNow;
    }

    public void UpdateExperienceLevel(string? level)
    {
        ExperienceLevel = level;
        LastUpdatedAt = DateTime.UtcNow;
    }

    public void UpdateCustomNotes(string? notes)
    {
        CustomNotes = notes;
        LastUpdatedAt = DateTime.UtcNow;
    }
}
