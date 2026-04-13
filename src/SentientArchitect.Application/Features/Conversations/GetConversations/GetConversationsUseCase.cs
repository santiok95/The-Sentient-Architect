using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;

namespace SentientArchitect.Application.Features.Conversations.GetConversations;

public class GetConversationsUseCase(IApplicationDbContext db)
{
    public async Task<Result<GetConversationsResponse>> ExecuteAsync(
        GetConversationsRequest request,
        CancellationToken ct = default)
    {
        var conversations = await db.Conversations
            .Where(c => c.UserId == request.UserId)
            .Include(c => c.Messages)
            .AsNoTracking()
            .ToListAsync(ct);

        var summaries = conversations
            .Select(c => new ConversationSummary(
                c.Id,
                c.Title,
                c.AgentType.ToString(),
                c.ContextMode.ToString(),
                c.Status.ToString(),
                c.Messages.Count,
                c.CreatedAt,
                c.UpdatedAt))
            .ToList();

        return Result<GetConversationsResponse>.SuccessWith(new GetConversationsResponse(summaries, summaries.Count));
    }
}
