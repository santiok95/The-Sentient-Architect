using SentientArchitect.Domain.Abstractions;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Domain.Entities;

public class TokenUsageTracker : BaseEntity
{
    public TokenUsageTracker(Guid userId, Guid tenantId, DateOnly date,
        long dailyQuota = 100_000, QuotaAction quotaAction = QuotaAction.Warn)
    {
        UserId = userId;
        TenantId = tenantId;
        Date = date;
        DailyQuota = dailyQuota;
        QuotaAction = quotaAction;
        TokensConsumed = 0;
        LastUpdatedAt = DateTime.UtcNow;
    }

    private TokenUsageTracker() { }

    public Guid UserId { get; private set; }
    public Guid TenantId { get; private set; }
    public DateOnly Date { get; private set; }
    public long TokensConsumed { get; private set; }
    public long DailyQuota { get; private set; }
    public QuotaAction QuotaAction { get; private set; }
    public DateTime LastUpdatedAt { get; private set; }

    public void ConsumeTokens(long amount)
    {
        TokensConsumed += amount;
        LastUpdatedAt = DateTime.UtcNow;
    }

    public void UpdateQuotaAction(QuotaAction action)
    {
        QuotaAction = action;
        LastUpdatedAt = DateTime.UtcNow;
    }

    public bool IsOverQuota() => TokensConsumed >= DailyQuota;

    public bool IsNearQuota(double threshold = 0.8) => TokensConsumed >= DailyQuota * threshold;
}
