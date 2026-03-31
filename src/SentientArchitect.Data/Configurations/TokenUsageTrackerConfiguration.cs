using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SentientArchitect.Domain.Entities;

namespace SentientArchitect.Data.Configurations;

public class TokenUsageTrackerConfiguration : IEntityTypeConfiguration<TokenUsageTracker>
{
    public void Configure(EntityTypeBuilder<TokenUsageTracker> builder)
    {
        builder.ToTable("TokenUsageTrackers");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.UserId).IsRequired();
        builder.Property(t => t.TenantId).IsRequired();
        builder.Property(t => t.Date).IsRequired();
        builder.Property(t => t.TokensConsumed).IsRequired();
        builder.Property(t => t.DailyQuota).IsRequired();
        builder.Property(t => t.QuotaAction).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(t => t.LastUpdatedAt).IsRequired();

        builder.HasIndex(t => new { t.UserId, t.Date }).IsUnique();
        builder.HasIndex(t => t.TenantId);
    }
}
