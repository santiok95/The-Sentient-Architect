using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;
using SentientArchitect.Domain.Entities;

namespace SentientArchitect.Application.Features.Conversations.CreateConversation;

public class CreateConversationUseCase(IApplicationDbContext db)
{
    public async Task<Result<CreateConversationResponse>> ExecuteAsync(
        CreateConversationRequest request,
        CancellationToken ct = default)
    {
        var conversation = new Conversation(request.UserId, request.TenantId, request.Title, request.AgentType);

        db.Conversations.Add(conversation);
        await db.SaveChangesAsync(ct);

        return Result<CreateConversationResponse>.SuccessWith(
            new CreateConversationResponse(conversation.Id, conversation.Title));
    }
}
