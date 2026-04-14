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
        // Project scalar values + SQL COUNT into an anonymous type, then transform in memory.
        // Two-step avoids translating .ToString() on enum conversions server-side (not supported by EF).
        var rows = await db.Conversations
            .Where(c => c.UserId == request.UserId)
            .AsNoTracking()
            .Select(c => new
            {
                c.Id,
                c.Title,
                c.AgentType,
                c.ContextMode,
                c.Status,
                MessageCount = c.Messages.Count(),  // translates to SQL COUNT — no message rows loaded
                c.CreatedAt,
                c.UpdatedAt
            })
            .ToListAsync(ct);

        var summaries = rows.Select(c => new ConversationSummary(
            c.Id,
            c.Title,
            c.AgentType.ToString(),
            c.ContextMode.ToString(),
            c.Status.ToString(),
            c.MessageCount,
            c.CreatedAt,
            c.UpdatedAt)).ToList();

        return Result<GetConversationsResponse>.SuccessWith(new GetConversationsResponse(summaries, summaries.Count));
    }
}
