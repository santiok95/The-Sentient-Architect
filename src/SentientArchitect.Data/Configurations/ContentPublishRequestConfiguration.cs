using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SentientArchitect.Domain.Entities;

namespace SentientArchitect.Data.Configurations;

public class ContentPublishRequestConfiguration : IEntityTypeConfiguration<ContentPublishRequest>
{
    public void Configure(EntityTypeBuilder<ContentPublishRequest> builder)
    {
        builder.ToTable("ContentPublishRequests");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.KnowledgeItemId).IsRequired();
        builder.Property(r => r.RequestedByUserId).IsRequired();
        builder.Property(r => r.Status).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(r => r.RequestReason).HasMaxLength(2000);
        builder.Property(r => r.RejectionReason).HasMaxLength(2000);
        builder.Property(r => r.CreatedAt).IsRequired();

        builder.HasOne(r => r.KnowledgeItem)
            .WithMany()
            .HasForeignKey(r => r.KnowledgeItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.RequestedByUserId);
    }
}
