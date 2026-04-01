using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Data;
using SentientArchitect.Domain.Entities;

namespace SentientArchitect.Data.Postgres.Repositories;

public class KnowledgeRepository(ApplicationContext context) : IKnowledgeRepository
{
    public async Task<KnowledgeItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.KnowledgeItems
            .Include(ki => ki.Embeddings)
            .Include(ki => ki.KnowledgeItemTags)
                .ThenInclude(kit => kit.Tag)
            .AsNoTracking()
            .FirstOrDefaultAsync(ki => ki.Id == id, ct);

    public async Task<IReadOnlyList<KnowledgeItem>> GetByUserAsync(Guid userId, CancellationToken ct = default)
        => await context.KnowledgeItems
            .Where(ki => ki.UserId == userId)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<IReadOnlyList<KnowledgeItem>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await context.KnowledgeItems
            .Where(ki => ki.TenantId == tenantId)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task AddAsync(KnowledgeItem item, CancellationToken ct = default)
    {
        await context.KnowledgeItems.AddAsync(item, ct);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(KnowledgeItem item, CancellationToken ct = default)
    {
        context.KnowledgeItems.Update(item);
        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var item = await context.KnowledgeItems.FindAsync([id], ct);
        if (item is not null)
        {
            context.KnowledgeItems.Remove(item);
            await context.SaveChangesAsync(ct);
        }
    }
}
