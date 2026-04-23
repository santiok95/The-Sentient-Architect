using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Application.Features.Conversations.CreateConversation;

public record CreateConversationRequest(
    Guid UserId,
    Guid TenantId,
    string Title = "Nueva conversación",
    AgentType AgentType = AgentType.Knowledge,
    Guid? ActiveRepositoryId = null);
