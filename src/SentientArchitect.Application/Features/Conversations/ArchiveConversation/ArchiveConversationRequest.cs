namespace SentientArchitect.Application.Features.Conversations.ArchiveConversation;

public record ArchiveConversationRequest(Guid ConversationId, Guid RequestingUserId);
