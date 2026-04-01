using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;

namespace SentientArchitect.Application.Features.Profile.RejectSuggestion;

public class RejectSuggestionUseCase(IApplicationDbContext db)
{
    public async Task<Result> ExecuteAsync(
        RejectSuggestionRequest request,
        CancellationToken ct = default)
    {
        var suggestion = await db.ProfileUpdateSuggestions
            .FirstOrDefaultAsync(s => s.Id == request.SuggestionId, ct);

        if (suggestion is null)
            return Result.Failure(["Suggestion not found."]);

        if (suggestion.UserId != request.UserId)
            return Result.Failure(["You do not have permission to reject this suggestion."]);

        suggestion.Reject();

        await db.SaveChangesAsync(ct);

        return Result.Success;
    }
}
