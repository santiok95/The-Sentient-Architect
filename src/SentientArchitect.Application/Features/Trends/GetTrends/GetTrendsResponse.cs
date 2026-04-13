using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Application.Features.Trends.GetTrends;

public record TrendItem(
    Guid Id,
    string Name,
    string Category,
    string TractionLevel,
    float RelevanceScore,
    string? Summary,
    List<string> Sources,
    string LastUpdatedAt,
    int? StarCount,
    string? GitHubUrl);

public record GetTrendsResponse(List<TrendItem> Items, int TotalCount, int Page, int PageSize);
