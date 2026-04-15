using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Application.Features.Conversations.Chat;

public sealed record ExecuteChatRequest(
    Guid ConversationId,
    Guid UserId,
    string Message,
    Guid? ActiveRepositoryId = null,
    string? PreferredStack = null,
    ConsultantContextMode? ContextMode = null);

public sealed record ChatExecutionRequest(
    Guid ConversationId,
    string Message,
    AgentType AgentType,
    Guid? ActiveRepositoryId = null,
    string? PreferredStack = null,
    ConsultantContextMode? ContextMode = null,
    bool ShouldCompact = false);

public sealed record ChatExecutionResponse(
    string AssistantMessage,
    AgentType AgentType);
