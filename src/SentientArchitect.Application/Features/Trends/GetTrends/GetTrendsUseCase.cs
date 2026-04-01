using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;

namespace SentientArchitect.Application.Features.Trends.GetTrends;

public class GetTrendsUseCase(IApplicationDbContext db)
{
    public async Task<Result<GetTrendsResponse>> ExecuteAsync(
        GetTrendsRequest request,
        CancellationToken ct = default)
    {
        var query = db.TechnologyTrends.AsNoTracking();

        if (request.Category.HasValue)
            query = query.Where(t => t.Category == request.Category.Value);

        var trends = await query.ToListAsync(ct);

        var items = trends
            .Select(t => new TrendItem(t.Id, t.Name, t.Category, t.Direction, t.RelevanceScore, t.Description))
            .ToList();

        return Result<GetTrendsResponse>.SuccessWith(new GetTrendsResponse(items));
    }
}
