using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;
using SentientArchitect.Domain.Entities;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Application.Features.Knowledge.RequestPublishKnowledge;

public class RequestPublishKnowledgeUseCase(IApplicationDbContext db)
{
    public async Task<Result<Guid>> ExecuteAsync(
        RequestPublishKnowledgeRequest request,
        CancellationToken ct = default)
    {
        var knowledgeItem = await db.KnowledgeItems
            .FirstOrDefaultAsync(k => k.Id == request.KnowledgeItemId, ct);

        if (knowledgeItem == null)
            return Result<Guid>.Failure(["Knowledge item not found."], ErrorType.NotFound);

        // Regla de Negocio 1: El item debe pertenecer al usuario que hace el request
        if (knowledgeItem.UserId != request.UserId)
            return Result<Guid>.Failure(["You can only request publishing for your own knowledge items."], ErrorType.Forbidden);

        // Regla de Negocio 2: El item no debe ser global/compartido ya
        if (knowledgeItem.IsShared)
            return Result<Guid>.Failure(["Knowledge item is already shared globally."], ErrorType.Validation);

        // Regla de Negocio 3: Idempotencia - No tener otro request pendiente en vuelo
        var existingRequest = await db.ContentPublishRequests
            .FirstOrDefaultAsync(r => r.KnowledgeItemId == request.KnowledgeItemId && r.Status == PublishRequestStatus.Pending, ct);

        if (existingRequest != null)
            return Result<Guid>.Failure(["A publish request is already pending for this item."], ErrorType.Validation);

        var publishRequest = new ContentPublishRequest(
            request.KnowledgeItemId,
            request.UserId,
            request.Reason);

        db.ContentPublishRequests.Add(publishRequest);
        await db.SaveChangesAsync(ct);

        return Result<Guid>.SuccessWith(publishRequest.Id);
    }
}
