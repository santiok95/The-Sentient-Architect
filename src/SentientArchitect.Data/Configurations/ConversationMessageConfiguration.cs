using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SentientArchitect.Domain.Entities;

namespace SentientArchitect.Data.Configurations;

public class ConversationMessageConfiguration : IEntityTypeConfiguration<ConversationMessage>
{
    public void Configure(EntityTypeBuilder<ConversationMessage> builder)
    {
        builder.ToTable("ConversationMessages");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.ConversationId).IsRequired();
        builder.Property(m => m.Role).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(m => m.Content).IsRequired();
        builder.Property(m => m.TokensUsed).IsRequired();
        builder.Property(m => m.CreatedAt).IsRequired();

        builder.Property(m => m.RetrievedContextIds)
            .HasColumnType("jsonb");

        builder.HasIndex(m => m.ConversationId);
    }
}
