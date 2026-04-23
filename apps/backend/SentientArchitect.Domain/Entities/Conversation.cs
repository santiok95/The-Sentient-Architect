using SentientArchitect.Domain.Abstractions;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Domain.Entities;

public class Conversation : BaseEntity
{
    public Conversation(Guid userId, Guid tenantId, string title = "Nueva conversación", AgentType agentType = AgentType.Knowledge)
    {
        UserId = userId;
        TenantId = tenantId;
        Title = title;
        AgentType = agentType;
        Status = ConversationStatus.Active;
        ContextMode = ConsultantContextMode.Auto;
        TokenCount = 0;
        UpdatedAt = DateTime.UtcNow;
        Messages = new HashSet<ConversationMessage>();
    }

    private Conversation()
    {
        Messages = new HashSet<ConversationMessage>();
    }

    public Guid UserId { get; private set; }
    public Guid TenantId { get; private set; }
    public string Title { get; private set; } = "New Conversation";
    public AgentType AgentType { get; private set; }
    public ConversationStatus Status { get; private set; }
    public ConsultantContextMode ContextMode { get; private set; }
    public Guid? ActiveRepositoryId { get; private set; }
    public string? ActiveRepositoryBranch { get; private set; }
    public string? PreferredStack { get; private set; }
    public string? Summary { get; private set; }
    public int TokenCount { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // Intent detected by LLM from the user's first message(s) in Auto mode
    public string? DetectedStack { get; private set; }
    public string? DetectedScope { get; private set; }  // "NewApp" | "ExistingRepo" | "Generic"

    // Tracks when the last compaction occurred so the threshold counts only post-compaction messages
    public DateTime? LastCompactedAt { get; private set; }

    public ICollection<ConversationMessage> Messages { get; private set; }

    public void AddMessage(ConversationMessage message)
    {
        Messages.Add(message);
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateSummary(string summary, int newTokenCount)
    {
        Summary = summary;
        TokenCount = newTokenCount;
        Status = ConversationStatus.Compacted;
        LastCompactedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        Status = ConversationStatus.Archived;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateTitle(string title)
    {
        Title = title;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateDetectedIntent(string? detectedStack, string? detectedScope)
    {
        if (!string.IsNullOrWhiteSpace(detectedStack))
            DetectedStack = detectedStack.Trim();
        if (!string.IsNullOrWhiteSpace(detectedScope))
            DetectedScope = detectedScope.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateConsultantContext(
        Guid? activeRepositoryId,
        string? preferredStack,
        ConsultantContextMode? contextMode = null,
        string? activeRepositoryBranch = null)
    {
        ActiveRepositoryId = activeRepositoryId;
        ActiveRepositoryBranch = activeRepositoryBranch;
        PreferredStack = string.IsNullOrWhiteSpace(preferredStack)
            ? null
            : preferredStack.Trim();

        if (contextMode.HasValue)
            ContextMode = contextMode.Value;

        UpdatedAt = DateTime.UtcNow;
    }
}
