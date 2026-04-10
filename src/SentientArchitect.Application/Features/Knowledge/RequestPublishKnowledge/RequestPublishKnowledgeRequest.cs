using SentientArchitect.Domain.Abstractions;

namespace SentientArchitect.Application.Features.Knowledge.RequestPublishKnowledge;

public record RequestPublishKnowledgeRequest(
    Guid UserId,
    Guid KnowledgeItemId,
    string? Reason = null);
