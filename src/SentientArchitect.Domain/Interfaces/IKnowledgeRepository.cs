using SentientArchitect.Domain.Entities;

namespace SentientArchitect.Domain.Interfaces;

/// <summary>
/// Repository contract for KnowledgeItem persistence.
/// Implementation lives in Infrastructure layer (EF Core).
/// </summary>
public interface IKnowledgeRepository
{
    Task<KnowledgeItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KnowledgeItem>> GetByUserAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task AddAsync(KnowledgeItem item, CancellationToken cancellationToken = default);
    Task UpdateAsync(KnowledgeItem item, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
