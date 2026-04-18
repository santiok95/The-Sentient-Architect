namespace SentientArchitect.Application.Common.Interfaces;

public interface IConversationStreamPublisher
{
    Task PublishTokenAsync(Guid conversationId, string token, CancellationToken ct = default);
    Task PublishCompleteAsync(Guid conversationId, CancellationToken ct = default);
    Task PublishErrorAsync(Guid conversationId, string message, CancellationToken ct = default);
}