using SentientArchitect.Domain.Entities;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Domain.Interfaces;

/// <summary>
/// Repository contract for Tag persistence.
/// Supports lookup by name+category (for deduplication during auto-tagging).
/// </summary>
public interface ITagRepository
{
    Task<Tag?> GetByNameAndCategoryAsync(
        string name,
        TagCategory category,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Tag>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Tag tag, CancellationToken cancellationToken = default);
}
