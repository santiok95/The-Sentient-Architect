using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;

namespace SentientArchitect.Application.Features.Conversations.DeleteConversation;

public record DeleteConversationRequest(Guid ConversationId);

public class DeleteConversationUseCase(IApplicationDbContext db)
{
    public async Task<Result> ExecuteAsync(DeleteConversationRequest request, CancellationToken ct = default)
    {
        var conversation = await db.Conversations.FindAsync(new object[] { request.ConversationId }, ct);
        if (conversation is not null)
        {
            db.Conversations.Remove(conversation);
            await db.SaveChangesAsync(ct);
        }

        return Result.Success;
    }
}
