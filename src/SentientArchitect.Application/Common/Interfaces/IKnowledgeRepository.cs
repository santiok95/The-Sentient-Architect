using SentientArchitect.Domain.Entities;

namespace SentientArchitect.Application.Common.Interfaces;

public interface IKnowledgeRepository
{
    Task<KnowledgeItem?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<KnowledgeItem>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<KnowledgeItem>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(KnowledgeItem item, CancellationToken ct = default);
    Task UpdateAsync(KnowledgeItem item, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
