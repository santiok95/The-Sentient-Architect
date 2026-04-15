using SentientArchitect.Domain.Abstractions;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Domain.Entities;

public class ProfileUpdateSuggestion : BaseEntity
{
    public ProfileUpdateSuggestion(Guid userId, string field,
        string suggestedValue, string reason, Guid? detectedInConversationId = null)
    {
        UserId = userId;
        Field = field;
        SuggestedValue = suggestedValue;
        Reason = reason;
        Status = SuggestionStatus.Pending;
        DetectedInConversationId = detectedInConversationId;
    }

    private ProfileUpdateSuggestion() { }

    public Guid UserId { get; private set; }
    public string Field { get; private set; } = string.Empty;
    public string SuggestedValue { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public SuggestionStatus Status { get; private set; }
    public Guid? DetectedInConversationId { get; private set; }

    public void Accept()
    {
        Status = SuggestionStatus.Accepted;
    }

    public void Reject()
    {
        Status = SuggestionStatus.Rejected;
    }
}
