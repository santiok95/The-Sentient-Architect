using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SentientArchitect.Domain.Entities;

namespace SentientArchitect.Infrastructure.Persistence.Configurations;

/// <summary>
/// Fluent API configuration for the KnowledgeItemTag join entity (M:N relationship).
/// Composite PK on (KnowledgeItemId, TagId).
/// </summary>
public class KnowledgeItemTagConfiguration : IEntityTypeConfiguration<KnowledgeItemTag>
{
    public void Configure(EntityTypeBuilder<KnowledgeItemTag> builder)
    {
        builder.ToTable("KnowledgeItemTags");

        // Composite primary key
        builder.HasKey(kit => new { kit.KnowledgeItemId, kit.TagId });

        // FK to Tag (cascade: if a tag is deleted, remove all associations)
        builder.HasOne(kit => kit.Tag)
            .WithMany()
            .HasForeignKey(kit => kit.TagId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK to KnowledgeItem is configured in KnowledgeItemConfiguration
        // (via HasMany(k => k.KnowledgeItemTags))
    }
}
