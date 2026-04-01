namespace SentientArchitect.Application.Features.Profile.AcceptSuggestion;

public record AcceptSuggestionRequest(Guid SuggestionId, Guid UserId);
