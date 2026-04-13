using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SentientArchitect.Domain.Entities;

namespace SentientArchitect.Data.Configurations;

public class TechnologyTrendConfiguration : IEntityTypeConfiguration<TechnologyTrend>
{
    public void Configure(EntityTypeBuilder<TechnologyTrend> builder)
    {
        builder.ToTable("TechnologyTrends");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Category).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(t => t.Direction).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(t => t.RelevanceScore).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(2000);
        builder.Property(t => t.Sources).IsRequired().HasColumnType("jsonb");
        builder.Property(t => t.LastScannedAt).IsRequired();
        builder.Property(t => t.StarCount);
        builder.Property(t => t.GitHubUrl).HasMaxLength(500);
        builder.Property(t => t.CreatedAt).IsRequired();

        builder.HasMany(t => t.Snapshots)
            .WithOne(s => s.TechnologyTrend)
            .HasForeignKey(s => s.TechnologyTrendId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.Name).IsUnique();
        builder.HasIndex(t => new { t.Category, t.Direction });
    }
}
