using SentientArchitect.Domain.Abstractions;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Domain.Entities;

public class Conversation : BaseEntity
{
    public Conversation(Guid userId, Guid tenantId, string title = "New Conversation")
    {
        UserId = userId;
        TenantId = tenantId;
        Title = title;
        Status = ConversationStatus.Active;
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
    public ConversationStatus Status { get; private set; }
    public string? Summary { get; private set; }
    public int TokenCount { get; private set; }
    public DateTime UpdatedAt { get; private set; }

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
}
