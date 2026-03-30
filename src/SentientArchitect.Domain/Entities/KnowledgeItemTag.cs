namespace SentientArchitect.Domain.Entities;

/// <summary>
/// Join entity for the M:N relationship between KnowledgeItem and Tag.
/// Composite PK (KnowledgeItemId, TagId) — configured via Fluent API in Infrastructure.
/// </summary>
public class KnowledgeItemTag
{
    /// <summary>EF Core requires a parameterless constructor. Never call this directly.</summary>
    private KnowledgeItemTag() { }

    public Guid KnowledgeItemId { get; private set; }
    public Guid TagId { get; private set; }

    // Navigation properties for EF Core
    public KnowledgeItem KnowledgeItem { get; private set; } = default!;
    public Tag Tag { get; private set; } = default!;

    /// <summary>
    /// Creates a new join record linking a KnowledgeItem to a Tag.
    /// </summary>
    public static KnowledgeItemTag Create(Guid knowledgeItemId, Guid tagId)
    {
        if (knowledgeItemId == Guid.Empty)
            throw new ArgumentException("KnowledgeItemId cannot be empty.", nameof(knowledgeItemId));

        if (tagId == Guid.Empty)
            throw new ArgumentException("TagId cannot be empty.", nameof(tagId));

        return new KnowledgeItemTag
        {
            KnowledgeItemId = knowledgeItemId,
            TagId = tagId
        };
    }
}
