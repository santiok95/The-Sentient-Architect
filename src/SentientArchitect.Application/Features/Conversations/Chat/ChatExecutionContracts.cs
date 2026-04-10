using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Application.Features.Conversations.Chat;

public sealed record ExecuteChatRequest(
    Guid ConversationId,
    Guid UserId,
    string Message,
    string AgentType = "Knowledge",
    Guid? ActiveRepositoryId = null,
    string? PreferredStack = null,
    ConsultantContextMode? ContextMode = null);

public sealed record ChatExecutionRequest(
    Guid ConversationId,
    string Message,
    string AgentType = "Knowledge",
    Guid? ActiveRepositoryId = null,
    string? PreferredStack = null,
    ConsultantContextMode? ContextMode = null);

public sealed record ChatExecutionResponse(
    string AssistantMessage,
    string AgentType);
