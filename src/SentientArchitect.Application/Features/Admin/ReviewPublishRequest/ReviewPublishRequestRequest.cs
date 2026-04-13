namespace SentientArchitect.Application.Features.Admin.ReviewPublishRequest;

public record ReviewPublishRequestRequest(
    Guid RequestId,
    Guid ReviewerUserId,
    string Action,
    string? RejectionReason = null);
