using SentientArchitect.Domain.Entities;

namespace SentientArchitect.Application.Common.Interfaces;

public interface ITagRepository
{
    Task<Tag?> GetByNameAndCategoryAsync(string name, Domain.Enums.TagCategory category,
        CancellationToken ct = default);
    Task<IReadOnlyList<Tag>> GetByKnowledgeItemAsync(Guid knowledgeItemId,
        CancellationToken ct = default);
    Task AddAsync(Tag tag, CancellationToken ct = default);
    Task AddTagToKnowledgeItemAsync(Guid knowledgeItemId, Guid tagId,
        CancellationToken ct = default);
}
