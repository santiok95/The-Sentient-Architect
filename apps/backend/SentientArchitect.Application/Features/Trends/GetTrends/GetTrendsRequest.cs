namespace SentientArchitect.Application.Features.Trends.GetTrends;

public record GetTrendsRequest(
    string? Category = null,
    string? Traction = null,
    int Page = 1,
    int PageSize = 20);
