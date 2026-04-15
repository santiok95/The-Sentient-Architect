using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;

namespace SentientArchitect.Application.Features.Knowledge.DeleteKnowledgeItem;

public record DeleteKnowledgeItemRequest(Guid KnowledgeItemId, Guid UserId);

public class DeleteKnowledgeItemUseCase(IApplicationDbContext db, IVectorStore vectorStore)
{
    public async Task<Result<bool>> ExecuteAsync(DeleteKnowledgeItemRequest request, CancellationToken ct = default)
    {
        var item = await db.KnowledgeItems.FindAsync([request.KnowledgeItemId], ct);

        if (item is null)
            return Result<bool>.SuccessWith(true); // idempotent — already gone

        if (item.UserId != request.UserId)
            return Result<bool>.Failure(["Access denied."], ErrorType.Forbidden);

        await vectorStore.DeleteByKnowledgeItemAsync(request.KnowledgeItemId, ct);
        db.KnowledgeItems.Remove(item);
        await db.SaveChangesAsync(ct);

        return Result<bool>.SuccessWith(true);
    }
}
