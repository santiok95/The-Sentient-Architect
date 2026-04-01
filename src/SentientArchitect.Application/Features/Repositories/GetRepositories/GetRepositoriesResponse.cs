using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Application.Features.Repositories.GetRepositories;

public record RepositoryItem(
    Guid Id,
    string Url,
    RepositoryTrust Trust,
    DateTime? LastAnalyzedAt,
    int ReportCount);

public record GetRepositoriesResponse(List<RepositoryItem> Repositories);
