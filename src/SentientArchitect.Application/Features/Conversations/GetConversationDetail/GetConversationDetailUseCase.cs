using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;
using static SentientArchitect.Application.Common.Results.ErrorType;

namespace SentientArchitect.Application.Features.Conversations.GetConversationDetail;

public class GetConversationDetailUseCase(IApplicationDbContext db)
{
    public async Task<Result<GetConversationDetailResponse>> ExecuteAsync(
        GetConversationDetailRequest request,
        CancellationToken ct = default)
    {
        // Load only the last 200 messages; full history is unbounded and causes OOM on long conversations
        var conversation = await db.Conversations
            .Include(c => c.Messages.OrderBy(m => m.CreatedAt).Take(200))
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.ConversationId, ct);

        if (conversation is null)
            return Result<GetConversationDetailResponse>.Failure(["Conversation not found."], ErrorType.NotFound);

        // Ownership check — conversation must belong to the requesting user
        if (conversation.UserId != request.UserId)
            return Result<GetConversationDetailResponse>.Failure(["Access denied."], ErrorType.Forbidden);

        var messages = conversation.Messages
            .OrderBy(m => m.CreatedAt)
            .Select(m => new ConversationMessageDto(
                m.Id,
                m.Role.ToString(),
                m.Content,
                m.CreatedAt,
                m.RetrievedContextIds))
            .ToList();

        // Use the branch persisted at conversation creation time.
        // Resolving from the live repo would change the context retroactively if the default branch changes.
        string? repoUrl    = null;
        string? repoBranch = conversation.ActiveRepositoryBranch;

        if (conversation.ActiveRepositoryId.HasValue)
        {
            repoUrl = await db.Repositories
                .AsNoTracking()
                .Where(r => r.Id == conversation.ActiveRepositoryId.Value)
                .Select(r => r.RepositoryUrl)
                .FirstOrDefaultAsync(ct);
        }

        var response = new GetConversationDetailResponse(
            conversation.Id,
            conversation.Title,
            conversation.AgentType.ToString(),
            conversation.ContextMode.ToString(),
            conversation.Status.ToString(),
            messages.Count,  // count of loaded messages (capped at 200)
            conversation.CreatedAt,
            conversation.UpdatedAt,
            messages,
            conversation.ActiveRepositoryId,
            repoUrl,
            repoBranch);

        return Result<GetConversationDetailResponse>.SuccessWith(response);
    }
}
