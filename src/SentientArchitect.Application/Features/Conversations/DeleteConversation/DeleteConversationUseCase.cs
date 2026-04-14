using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;

namespace SentientArchitect.Application.Features.Conversations.DeleteConversation;

public record DeleteConversationRequest(Guid ConversationId, Guid UserId);

public class DeleteConversationUseCase(IApplicationDbContext db)
{
    public async Task<Result> ExecuteAsync(DeleteConversationRequest request, CancellationToken ct = default)
    {
        var conversation = await db.Conversations.FindAsync(new object[] { request.ConversationId }, ct);

        if (conversation is null)
            return Result.Success; // idempotent — already gone

        if (conversation.UserId != request.UserId)
            return Result.Failure(["Access denied."], ErrorType.Forbidden);

        db.Conversations.Remove(conversation);
        await db.SaveChangesAsync(ct);

        return Result.Success;
    }
}
