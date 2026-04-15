using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Application.Features.Trends.GetTrends;

public class GetTrendsUseCase(IApplicationDbContext db)
{
    public async Task<Result<GetTrendsResponse>> ExecuteAsync(
        GetTrendsRequest request,
        CancellationToken ct = default)
    {
        var baseQuery = db.TechnologyTrends.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Category) &&
            Enum.TryParse<TrendCategory>(request.Category, ignoreCase: true, out var cat))
            baseQuery = baseQuery.Where(t => t.Category == cat);

        var totalCount = await baseQuery.CountAsync(ct);

        var query = baseQuery.OrderByDescending(t => t.RelevanceScore);

        var page     = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var trends = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // Map TrendDirection to frontend TractionLevel
        static string ToTraction(TrendDirection d) => d switch
        {
            TrendDirection.Rising   => "Growing",
            TrendDirection.Stable   => "Mainstream",
            TrendDirection.Declining => "Declining",
            _                       => "Mainstream",
        };

        // Apply traction filter in-memory after DB query (enum mapping makes EF translation complex)
        var filtered = string.IsNullOrWhiteSpace(request.Traction)
            ? trends
            : trends.Where(t => ToTraction(t.Direction).Equals(request.Traction, StringComparison.OrdinalIgnoreCase)).ToList();

        var items = filtered.Select(t => new TrendItem(
            t.Id,
            t.Name,
            t.Category.ToString(),
            ToTraction(t.Direction),
            t.RelevanceScore * 100f,   // normalize to 0–100 for the score bar
            t.Description,
            t.Sources,
            t.LastScannedAt.ToString("O"),
            t.StarCount,
            t.GitHubUrl)).ToList();

        return Result<GetTrendsResponse>.SuccessWith(
            new GetTrendsResponse(items, totalCount, page, pageSize));
    }
}
