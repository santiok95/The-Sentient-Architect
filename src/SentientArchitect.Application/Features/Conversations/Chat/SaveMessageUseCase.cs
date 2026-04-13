using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;
using SentientArchitect.Domain.Entities;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Application.Features.Conversations.Chat;

public record SaveMessageRequest(Guid ConversationId, Guid UserId, string Message, MessageRole Role);

public class SaveMessageUseCase(IApplicationDbContext db)
{
    public async Task<Result<List<ConversationMessage>>> ExecuteAsync(
        SaveMessageRequest request,
        Conversation? existingConversation = null,
        CancellationToken ct = default)
    {
        // Reuse the already-tracked conversation when provided to avoid double-load
        // concurrency issues in the same DbContext scope. Caller must include Messages.
        var conversation = existingConversation ?? await db.Conversations
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == request.ConversationId, ct);

        if (conversation is null || conversation.UserId != request.UserId)
            return Result<List<ConversationMessage>>.Failure(["Conversation not found."]);

        var message = new ConversationMessage(request.ConversationId, request.Role, request.Message);
        conversation.AddMessage(message);
        db.ConversationMessages.Add(message);

        await db.SaveChangesAsync(ct);

        var recentMessages = conversation.Messages
            .OrderBy(m => m.CreatedAt)
            .TakeLast(20)
            .ToList();

        return Result<List<ConversationMessage>>.SuccessWith(recentMessages);
    }
}
