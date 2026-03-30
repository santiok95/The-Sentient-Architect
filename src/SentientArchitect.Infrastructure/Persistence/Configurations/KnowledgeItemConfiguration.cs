using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SentientArchitect.Domain.Entities;
using SentientArchitect.Domain.Enums;
using SentientArchitect.Infrastructure.Identity;

namespace SentientArchitect.Infrastructure.Persistence.Configurations;

/// <summary>
/// Fluent API configuration for KnowledgeItem.
/// 
/// FK to User configured without navigation property (ADR-002):
/// KnowledgeItem lives in Domain and cannot reference the User entity from Infrastructure.
/// The FK constraint is enforced at database level via migrations.
/// </summary>
public class KnowledgeItemConfiguration : IEntityTypeConfiguration<KnowledgeItem>
{
    public void Configure(EntityTypeBuilder<KnowledgeItem> builder)
    {
        builder.ToTable("KnowledgeItems");

        builder.HasKey(k => k.Id);

        // --- Scalar Properties ---

        builder.Property(k => k.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(k => k.OriginalContent)
            .IsRequired();

        builder.Property(k => k.Summary);

        builder.Property(k => k.SourceUrl)
            .HasMaxLength(2048);

        builder.Property(k => k.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(k => k.ProcessingStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(k => k.UserId)
            .IsRequired();

        builder.Property(k => k.TenantId)
            .IsRequired();

        builder.Property(k => k.CreatedAt)
            .IsRequired();

        builder.Property(k => k.UpdatedAt)
            .IsRequired();

        // --- FK to User (ADR-002: no navigation property) ---
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(k => k.UserId)
            .OnDelete(DeleteBehavior.Restrict); // Don't cascade-delete knowledge when user is deleted

        // --- Relationships ---

        builder.HasMany(k => k.Embeddings)
            .WithOne()
            .HasForeignKey(e => e.KnowledgeItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(k => k.KnowledgeItemTags)
            .WithOne(kit => kit.KnowledgeItem)
            .HasForeignKey(kit => kit.KnowledgeItemId)
            .OnDelete(DeleteBehavior.Cascade);

        // --- Indexes (per entity-framework.md) ---

        builder.HasIndex(k => new { k.UserId, k.TenantId });

        builder.HasIndex(k => k.Type);
    }
}
