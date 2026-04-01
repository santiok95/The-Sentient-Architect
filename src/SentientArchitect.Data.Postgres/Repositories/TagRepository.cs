using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Data;
using SentientArchitect.Domain.Entities;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Data.Postgres.Repositories;

public class TagRepository(ApplicationContext context) : ITagRepository
{
    public async Task<Tag?> GetByNameAndCategoryAsync(string name, TagCategory category, CancellationToken ct = default)
        => await context.Tags
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Name == name && t.Category == category, ct);

    public async Task<IReadOnlyList<Tag>> GetByKnowledgeItemAsync(Guid knowledgeItemId, CancellationToken ct = default)
        => await context.KnowledgeItemTags
            .Where(kit => kit.KnowledgeItemId == knowledgeItemId)
            .Select(kit => kit.Tag!)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task AddAsync(Tag tag, CancellationToken ct = default)
    {
        await context.Tags.AddAsync(tag, ct);
        await context.SaveChangesAsync(ct);
    }

    public async Task AddTagToKnowledgeItemAsync(Guid knowledgeItemId, Guid tagId, CancellationToken ct = default)
    {
        var link = new KnowledgeItemTag
        {
            KnowledgeItemId = knowledgeItemId,
            TagId = tagId
        };

        await context.KnowledgeItemTags.AddAsync(link, ct);
        await context.SaveChangesAsync(ct);
    }
}
