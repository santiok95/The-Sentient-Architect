using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SentientArchitect.Domain.Entities;

namespace SentientArchitect.Data.Configurations;

public class KnowledgeItemConfiguration : IEntityTypeConfiguration<KnowledgeItem>
{
    public void Configure(EntityTypeBuilder<KnowledgeItem> builder)
    {
        builder.ToTable("KnowledgeItems");
        builder.HasKey(k => k.Id);

        builder.Property(k => k.Title).IsRequired().HasMaxLength(500);
        builder.Property(k => k.OriginalContent).IsRequired();
        builder.Property(k => k.SourceUrl).HasMaxLength(2048);
        builder.Property(k => k.Type).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(k => k.ProcessingStatus).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(k => k.UserId).IsRequired();
        builder.Property(k => k.TenantId).IsRequired();
        builder.Property(k => k.CreatedAt).IsRequired();
        builder.Property(k => k.UpdatedAt).IsRequired();

        // FK to ApplicationUser (ADR-002: no navigation property in Domain)
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(k => k.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(k => k.Embeddings)
            .WithOne(e => e.KnowledgeItem)
            .HasForeignKey(e => e.KnowledgeItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(k => k.KnowledgeItemTags)
            .WithOne(kit => kit.KnowledgeItem)
            .HasForeignKey(kit => kit.KnowledgeItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(k => new { k.UserId, k.TenantId });
        builder.HasIndex(k => k.Type);
    }
}
