using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;
using MediatR; 

namespace SentientArchitect.Application.Features.Knowledge.DeleteKnowledgeItem;

public record DeleteKnowledgeItemRequest(Guid KnowledgeItemId);

public class DeleteKnowledgeItemUseCase(IApplicationDbContext db, IVectorStore vectorStore)
{
    public async Task<Result<bool>> ExecuteAsync(DeleteKnowledgeItemRequest request, CancellationToken ct = default)
    {
        await vectorStore.DeleteByKnowledgeItemAsync(request.KnowledgeItemId, ct);

        var item = await db.KnowledgeItems.FindAsync([request.KnowledgeItemId], ct);
        if (item is not null)
        {
            db.KnowledgeItems.Remove(item);
            await db.SaveChangesAsync(ct);
        }

        return Result<bool>.SuccessWith(true);
    }
}
