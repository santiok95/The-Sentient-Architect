using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SentientArchitect.Domain.Entities;

namespace SentientArchitect.Data.Configurations;

public class AnalysisReportConfiguration : IEntityTypeConfiguration<AnalysisReport>
{
    public void Configure(EntityTypeBuilder<AnalysisReport> builder)
    {
        builder.ToTable("AnalysisReports");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Status).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(a => a.ErrorMessage).HasMaxLength(2000);
        builder.Property(a => a.Summary);
        builder.Property(a => a.TotalFindings).IsRequired();
        builder.Property(a => a.CriticalFindings).IsRequired();
        builder.Property(a => a.RepositoryInfoId).IsRequired();
        builder.Property(a => a.CreatedAt).IsRequired();

        builder.HasMany(a => a.Findings)
            .WithOne(f => f.AnalysisReport)
            .HasForeignKey(f => f.AnalysisReportId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.RepositoryInfoId);
    }
}
