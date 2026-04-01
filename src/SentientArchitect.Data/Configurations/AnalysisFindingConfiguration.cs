using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SentientArchitect.Domain.Entities;

namespace SentientArchitect.Data.Configurations;

public class AnalysisFindingConfiguration : IEntityTypeConfiguration<AnalysisFinding>
{
    public void Configure(EntityTypeBuilder<AnalysisFinding> builder)
    {
        builder.ToTable("AnalysisFindings");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Severity).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(f => f.Category).IsRequired().HasMaxLength(100);
        builder.Property(f => f.Message).IsRequired();
        builder.Property(f => f.FilePath).HasMaxLength(500);
        builder.Property(f => f.AnalysisReportId).IsRequired();
        builder.Property(f => f.CreatedAt).IsRequired();

        builder.HasIndex(f => new { f.AnalysisReportId, f.Severity });
    }
}
