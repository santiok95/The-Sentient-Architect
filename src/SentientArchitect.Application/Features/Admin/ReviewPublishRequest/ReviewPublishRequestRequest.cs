namespace SentientArchitect.Application.Features.Admin.ReviewPublishRequest;

public record ReviewPublishRequestRequest(
    Guid RequestId,
    Guid ReviewerUserId,
    bool Approved,
    string? RejectionReason = null);
