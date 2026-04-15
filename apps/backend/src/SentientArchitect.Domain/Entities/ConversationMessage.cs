using SentientArchitect.Domain.Abstractions;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Domain.Entities;

public class ConversationMessage : BaseEntity
{
    public ConversationMessage(Guid conversationId, MessageRole role, string content, int tokensUsed = 0)
    {
        ConversationId = conversationId;
        Role = role;
        Content = content;
        TokensUsed = tokensUsed;
        RetrievedContextIds = [];
    }

    private ConversationMessage()
    {
        RetrievedContextIds = [];
    }

    public Guid ConversationId { get; private set; }
    public MessageRole Role { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public int TokensUsed { get; private set; }
    public List<Guid> RetrievedContextIds { get; private set; }

    public Conversation? Conversation { get; private set; }
}
