using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SentientArchitect.Domain.Entities;

namespace SentientArchitect.Data.Configurations;

public class TrendSnapshotConfiguration : IEntityTypeConfiguration<TrendSnapshot>
{
    public void Configure(EntityTypeBuilder<TrendSnapshot> builder)
    {
        builder.ToTable("TrendSnapshots");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.TechnologyTrendId).IsRequired();
        builder.Property(s => s.Score).IsRequired();
        builder.Property(s => s.Direction).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(s => s.Date).IsRequired();
        builder.Property(s => s.Notes).HasMaxLength(1000);
        builder.Property(s => s.CreatedAt).IsRequired();

        builder.HasIndex(s => new { s.TechnologyTrendId, s.Date }).IsUnique();
    }
}
