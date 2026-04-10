using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SentientArchitect.Domain.Entities;

namespace SentientArchitect.Data.Configurations;

public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("Conversations");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.UserId).IsRequired();
        builder.Property(c => c.TenantId).IsRequired();
        builder.Property(c => c.Title).IsRequired().HasMaxLength(500);
        builder.Property(c => c.Status).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(c => c.ContextMode).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(c => c.ActiveRepositoryId);
        builder.Property(c => c.PreferredStack).HasMaxLength(120);
        builder.Property(c => c.TokenCount).IsRequired();
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt).IsRequired();

        // FK to ApplicationUser (ADR-002: no navigation property in Domain)
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<RepositoryInfo>()
            .WithMany()
            .HasForeignKey(c => c.ActiveRepositoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(c => c.Messages)
            .WithOne(m => m.Conversation)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => new { c.UserId, c.Status });
        builder.HasIndex(c => c.ActiveRepositoryId);
    }
}
