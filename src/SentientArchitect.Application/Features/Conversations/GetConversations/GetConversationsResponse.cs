using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Application.Features.Conversations.GetConversations;

public record ConversationSummary(
    Guid Id,
    string Title,
    ConversationStatus Status,
    int TokenCount,
    DateTime UpdatedAt);

public record GetConversationsResponse(List<ConversationSummary> Conversations);
