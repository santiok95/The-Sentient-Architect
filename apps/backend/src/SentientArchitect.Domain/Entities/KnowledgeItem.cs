using SentientArchitect.Domain.Abstractions;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Domain.Entities;

public class KnowledgeItem : BaseEntity
{
    public KnowledgeItem(Guid userId, Guid tenantId, string title,
        string originalContent, KnowledgeItemType type, string? sourceUrl = null)
    {
        UserId = userId;
        TenantId = tenantId;
        Title = title;
        OriginalContent = originalContent;
        Type = type;
        SourceUrl = sourceUrl;
        ProcessingStatus = ProcessingStatus.Pending;
        UpdatedAt = DateTime.UtcNow;
        Embeddings = new HashSet<KnowledgeEmbedding>();
        KnowledgeItemTags = new HashSet<KnowledgeItemTag>();
    }

    private KnowledgeItem()
    {
        Embeddings = [];
        KnowledgeItemTags = [];
    }

    public Guid UserId { get; private set; }
    public Guid TenantId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? SourceUrl { get; private set; }
    public string OriginalContent { get; private set; } = string.Empty;
    public string? Summary { get; private set; }
    public KnowledgeItemType Type { get; private set; }
    public ProcessingStatus ProcessingStatus { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public ICollection<KnowledgeEmbedding> Embeddings { get; private set; }
    public ICollection<KnowledgeItemTag> KnowledgeItemTags { get; private set; }

    public void MarkAsProcessing()
    {
        ProcessingStatus = ProcessingStatus.Processing;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsCompleted(string summary)
    {
        ProcessingStatus = ProcessingStatus.Completed;
        Summary = summary;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsFailed()
    {
        ProcessingStatus = ProcessingStatus.Failed;
        UpdatedAt = DateTime.UtcNow;
    }

    public void PublishToShared(Guid sharedTenantId)
    {
        TenantId = sharedTenantId;
        UpdatedAt = DateTime.UtcNow;
    }
}
