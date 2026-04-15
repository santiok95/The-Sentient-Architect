using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Application.Features.Conversations.GetConversations;

public record ConversationSummary(
    Guid Id,
    string Title,
    string AgentType,
    string Mode,
    string Status,
    int MessageCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record GetConversationsResponse(List<ConversationSummary> Items, int TotalCount);
