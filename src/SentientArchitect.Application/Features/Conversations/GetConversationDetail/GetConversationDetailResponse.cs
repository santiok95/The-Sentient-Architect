using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Application.Features.Conversations.GetConversationDetail;

public record ConversationMessageDto(
    Guid Id,
    string Role,
    string Content,
    DateTime CreatedAt,
    List<Guid> RetrievedContextIds);

public record GetConversationDetailResponse(
    Guid Id,
    string Objective,
    string Mode,
    string Status,
    int MessageCount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<ConversationMessageDto> RecentMessages);
