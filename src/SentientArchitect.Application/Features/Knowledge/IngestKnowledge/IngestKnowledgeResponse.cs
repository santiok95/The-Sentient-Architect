using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Application.Features.Knowledge.IngestKnowledge;

public record IngestKnowledgeResponse(
    Guid KnowledgeItemId,
    ProcessingStatus Status,
    int ChunksCreated
);
