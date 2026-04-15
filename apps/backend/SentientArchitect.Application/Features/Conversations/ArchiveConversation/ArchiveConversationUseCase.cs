using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;

namespace SentientArchitect.Application.Features.Conversations.ArchiveConversation;

public class ArchiveConversationUseCase(IApplicationDbContext db)
{
    public async Task<Result> ExecuteAsync(
        ArchiveConversationRequest request,
        CancellationToken ct = default)
    {
        var conversation = await db.Conversations
            .FirstOrDefaultAsync(c => c.Id == request.ConversationId, ct);

        if (conversation is null)
            return Result.Failure(["Conversation not found."]);

        if (conversation.UserId != request.RequestingUserId)
            return Result.Failure(["You do not have permission to archive this conversation."]);

        conversation.Archive();

        await db.SaveChangesAsync(ct);

        return Result.Success;
    }
}
