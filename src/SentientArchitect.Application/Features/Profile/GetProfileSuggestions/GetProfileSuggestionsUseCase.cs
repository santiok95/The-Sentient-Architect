using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;
using SentientArchitect.Domain.Entities;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Application.Features.Profile.GetProfileSuggestions;

public record GetProfileSuggestionsRequest(Guid UserId);

public class GetProfileSuggestionsUseCase(IApplicationDbContext db)
{
    public async Task<Result<List<ProfileUpdateSuggestion>>> ExecuteAsync(GetProfileSuggestionsRequest request, CancellationToken ct = default)
    {
        var suggestions = await db.ProfileUpdateSuggestions
            .Where(s => s.UserId == request.UserId && s.Status == SuggestionStatus.Pending)
            .AsNoTracking()
            .ToListAsync(ct);

        return Result<List<ProfileUpdateSuggestion>>.SuccessWith(suggestions);
    }
}
