using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Application.Features.Knowledge.IngestKnowledge;

public record IngestKnowledgeRequest(
    Guid UserId,
    Guid TenantId,
    string Title,
    string OriginalContent,
    KnowledgeItemType Type,
    string? SourceUrl = null,
    List<string>? Tags = null,
    bool IsUserAdmin = false
);
