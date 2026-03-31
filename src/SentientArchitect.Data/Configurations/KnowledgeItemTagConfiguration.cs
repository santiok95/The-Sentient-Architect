using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SentientArchitect.Domain.Entities;

namespace SentientArchitect.Data.Configurations;

public class KnowledgeItemTagConfiguration : IEntityTypeConfiguration<KnowledgeItemTag>
{
    public void Configure(EntityTypeBuilder<KnowledgeItemTag> builder)
    {
        builder.ToTable("KnowledgeItemTags");
        builder.HasKey(kit => new { kit.KnowledgeItemId, kit.TagId });

        builder.HasOne(kit => kit.Tag)
            .WithMany(t => t.KnowledgeItemTags)
            .HasForeignKey(kit => kit.TagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
