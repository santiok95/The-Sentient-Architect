namespace SentientArchitect.Application.Features.Conversations.CreateConversation;

public record CreateConversationRequest(Guid UserId, Guid TenantId, string Title = "New Conversation");
