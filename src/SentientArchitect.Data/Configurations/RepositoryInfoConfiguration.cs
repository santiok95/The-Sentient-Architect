using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SentientArchitect.Domain.Entities;

namespace SentientArchitect.Data.Configurations;

public class RepositoryInfoConfiguration : IEntityTypeConfiguration<RepositoryInfo>
{
    public void Configure(EntityTypeBuilder<RepositoryInfo> builder)
    {
        builder.ToTable("Repositories");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.RepositoryUrl).IsRequired().HasMaxLength(2048);
        builder.Property(r => r.LocalPath).HasMaxLength(1024);
        builder.Property(r => r.Trust).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(r => r.DefaultBranch).HasMaxLength(255);
        builder.Property(r => r.PrimaryLanguage).HasMaxLength(100);
        builder.Property(r => r.UserId).IsRequired();
        builder.Property(r => r.TenantId).IsRequired();
        builder.Property(r => r.CreatedAt).IsRequired();

        // FK to ApplicationUser (ADR-002: no navigation property in Domain)
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Reports)
            .WithOne(a => a.RepositoryInfo)
            .HasForeignKey(a => a.RepositoryInfoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.UserId);
    }
}
