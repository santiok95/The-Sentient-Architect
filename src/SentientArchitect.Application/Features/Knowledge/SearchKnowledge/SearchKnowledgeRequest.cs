namespace SentientArchitect.Application.Features.Knowledge.SearchKnowledge;

public record SearchKnowledgeRequest(
    Guid UserId,
    Guid TenantId,
    string Query,
    int MaxResults = 5,
    bool IncludeShared = true,
    float MinimumScore = 0.7f
);
