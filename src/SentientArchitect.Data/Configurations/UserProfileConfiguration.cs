using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SentientArchitect.Domain.Entities;

namespace SentientArchitect.Data.Configurations;

public class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("UserProfiles");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.UserId).IsRequired();
        builder.Property(p => p.InfrastructureContext).HasMaxLength(2000);
        builder.Property(p => p.TeamSize).HasMaxLength(100);
        builder.Property(p => p.ExperienceLevel).HasMaxLength(100);
        builder.Property(p => p.CustomNotes).HasMaxLength(5000);
        builder.Property(p => p.LastUpdatedAt).IsRequired();

        // List<string> → JSONB (PostgreSQL) / JSON (SQL Server)
        builder.Property(p => p.PreferredStack)
            .HasColumnType("jsonb");

        builder.Property(p => p.KnownPatterns)
            .HasColumnType("jsonb");

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.UserId).IsUnique();
    }
}
