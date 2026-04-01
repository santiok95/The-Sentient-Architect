using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;
using SentientArchitect.Domain.Entities;

namespace SentientArchitect.Application.Features.Knowledge.GetKnowledgeItems;

public record GetKnowledgeItemsRequest(Guid UserId);

public class GetKnowledgeItemsUseCase(IApplicationDbContext db)
{
    public async Task<Result<List<KnowledgeItem>>> ExecuteAsync(GetKnowledgeItemsRequest request, CancellationToken ct = default)
    {
        var items = await db.KnowledgeItems
            .Where(k => k.UserId == request.UserId)
            .AsNoTracking()
            .ToListAsync(ct);

        return Result<List<KnowledgeItem>>.SuccessWith(items);
    }
}
