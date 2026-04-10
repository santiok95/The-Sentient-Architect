using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;

namespace SentientArchitect.Application.Features.Conversations.GetConversationDetail;

public class GetConversationDetailUseCase(IApplicationDbContext db)
{
    public async Task<Result<GetConversationDetailResponse>> ExecuteAsync(
        GetConversationDetailRequest request,
        CancellationToken ct = default)
    {
        var conversation = await db.Conversations
            .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.ConversationId, ct);

        if (conversation is null)
            return Result<GetConversationDetailResponse>.Failure("Conversation not found");

        var messages = conversation.Messages
            .Select(m => new ConversationMessageDto(
                m.Id,
                m.Role.ToString(),
                m.Content,
                m.CreatedAt,
                m.RetrievedContextIds))
            .ToList();

        var response = new GetConversationDetailResponse(
            conversation.Id,
            conversation.Title,
            conversation.ContextMode.ToString(),
            conversation.Status.ToString(),
            conversation.Messages.Count,
            conversation.CreatedAt,
            conversation.UpdatedAt,
            messages);

        return Result<GetConversationDetailResponse>.SuccessWith(response);
    }
}
