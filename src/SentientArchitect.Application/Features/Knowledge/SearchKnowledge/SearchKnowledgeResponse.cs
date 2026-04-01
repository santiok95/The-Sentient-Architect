using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Application.Features.Knowledge.SearchKnowledge;

public record SearchKnowledgeResponse(
    List<KnowledgeSearchResult> Results,
    int TotalFound
);

public record KnowledgeSearchResult(
    Guid KnowledgeItemId,
    string Title,
    string ChunkText,
    float Score,
    KnowledgeItemType Type,
    string? SourceUrl
);
