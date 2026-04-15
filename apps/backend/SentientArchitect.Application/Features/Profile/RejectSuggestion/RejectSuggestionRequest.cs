namespace SentientArchitect.Application.Features.Profile.RejectSuggestion;

public record RejectSuggestionRequest(Guid SuggestionId, Guid UserId);
