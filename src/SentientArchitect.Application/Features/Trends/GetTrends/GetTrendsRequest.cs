using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Application.Features.Trends.GetTrends;

public record GetTrendsRequest(TrendCategory? Category = null);
