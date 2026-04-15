using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Application.Features.Repositories.SubmitRepository;

public record SubmitRepositoryRequest(
    Guid UserId,
    Guid TenantId,
    string RepositoryUrl,
    RepositoryTrust Trust);
