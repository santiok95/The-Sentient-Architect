using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SentientArchitect.Domain.Entities;

namespace SentientArchitect.Data.Configurations;

public class ProfileUpdateSuggestionConfiguration : IEntityTypeConfiguration<ProfileUpdateSuggestion>
{
    public void Configure(EntityTypeBuilder<ProfileUpdateSuggestion> builder)
    {
        builder.ToTable("ProfileUpdateSuggestions");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.UserId).IsRequired();
        builder.Property(s => s.Field).IsRequired().HasMaxLength(200);
        builder.Property(s => s.SuggestedValue).IsRequired().HasMaxLength(2000);
        builder.Property(s => s.Reason).IsRequired().HasMaxLength(2000);
        builder.Property(s => s.Status).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(s => s.CreatedAt).IsRequired();

        builder.HasIndex(s => s.UserId);
    }
}
