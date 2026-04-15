namespace SentientArchitect.Application.Features.Repositories.GetRepositories;

public record RepositoryItem(
    Guid Id,
    string GitUrl,
    string TrustLevel,
    string? PrimaryLanguage,
    int? Stars,
    string? LastCommitDate,
    string ProcessingStatus,
    string Scope,
    string CreatedAt);

public record GetRepositoriesResponse(List<RepositoryItem> Items, int TotalCount);
