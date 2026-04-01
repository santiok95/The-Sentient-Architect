using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Application.Features.Trends.GetTrends;

public record TrendItem(
    Guid Id,
    string Name,
    TrendCategory Category,
    TrendDirection Direction,
    float RelevanceScore,
    string? Description);

public record GetTrendsResponse(List<TrendItem> Trends);
