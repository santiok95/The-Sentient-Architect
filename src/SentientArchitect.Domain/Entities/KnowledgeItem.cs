using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Domain.Entities;

/// <summary>
/// Central entity of the Semantic Brain. All knowledge flows through this.
/// Created via factory method to guarantee a valid initial state.
/// </summary>
public class KnowledgeItem
{
    private readonly List<KnowledgeEmbedding> _embeddings = [];
    private readonly List<KnowledgeItemTag> _knowledgeItemTags = [];

    /// <summary>EF Core requires a parameterless constructor. Never call this directly.</summary>
    private KnowledgeItem() { }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid TenantId { get; private set; }
    public string Title { get; private set; } = default!;
    public string? SourceUrl { get; private set; }
    public string OriginalContent { get; private set; } = default!;
    public string? Summary { get; private set; }
    public KnowledgeItemType Type { get; private set; }
    public ProcessingStatus ProcessingStatus { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public IReadOnlyCollection<KnowledgeEmbedding> Embeddings => _embeddings.AsReadOnly();
    public IReadOnlyCollection<KnowledgeItemTag> KnowledgeItemTags => _knowledgeItemTags.AsReadOnly();

    /// <summary>
    /// Creates a new KnowledgeItem in Pending state.
    /// </summary>
    public static KnowledgeItem Create(
        Guid userId,
        Guid tenantId,
        string title,
        string originalContent,
        KnowledgeItemType type,
        string? sourceUrl = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty.", nameof(userId));

        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));

        if (string.IsNullOrWhiteSpace(originalContent))
            throw new ArgumentException("OriginalContent cannot be empty.", nameof(originalContent));

        var now = DateTime.UtcNow;

        return new KnowledgeItem
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = tenantId,
            Title = title.Trim(),
            OriginalContent = originalContent,
            Type = type,
            SourceUrl = sourceUrl?.Trim(),
            ProcessingStatus = ProcessingStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Sets the AI-generated summary after ingestion processing.
    /// </summary>
    public void UpdateSummary(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
            throw new ArgumentException("Summary cannot be empty.", nameof(summary));

        Summary = summary.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Transitions the processing status (Pending → Processing → Completed/Failed).
    /// </summary>
    public void MarkAs(ProcessingStatus status)
    {
        ProcessingStatus = status;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the mutable content fields (title and original content).
    /// </summary>
    public void UpdateContent(string title, string originalContent)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));

        if (string.IsNullOrWhiteSpace(originalContent))
            throw new ArgumentException("OriginalContent cannot be empty.", nameof(originalContent));

        Title = title.Trim();
        OriginalContent = originalContent;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the TenantId — used during content publication approval
    /// when a personal item becomes shared.
    /// </summary>
    public void PublishToTenant(Guid sharedTenantId)
    {
        if (sharedTenantId == Guid.Empty)
            throw new ArgumentException("SharedTenantId cannot be empty.", nameof(sharedTenantId));

        TenantId = sharedTenantId;
        UpdatedAt = DateTime.UtcNow;
    }
}
