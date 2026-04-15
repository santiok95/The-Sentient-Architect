using SentientArchitect.Domain.Abstractions;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Domain.Entities;

public class ContentPublishRequest : BaseEntity
{
    public ContentPublishRequest(Guid knowledgeItemId, Guid requestedByUserId,
        string? requestReason = null)
    {
        KnowledgeItemId = knowledgeItemId;
        RequestedByUserId = requestedByUserId;
        RequestReason = requestReason;
        Status = PublishRequestStatus.Pending;
    }

    private ContentPublishRequest() { }

    public Guid KnowledgeItemId { get; private set; }
    public Guid RequestedByUserId { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public PublishRequestStatus Status { get; private set; }
    public string? RequestReason { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime? ReviewedAt { get; private set; }

    public KnowledgeItem? KnowledgeItem { get; private set; }

    public void Approve(Guid reviewerUserId)
    {
        Status = PublishRequestStatus.Approved;
        ReviewedByUserId = reviewerUserId;
        ReviewedAt = DateTime.UtcNow;
    }

    public void Reject(Guid reviewerUserId, string reason)
    {
        Status = PublishRequestStatus.Rejected;
        ReviewedByUserId = reviewerUserId;
        RejectionReason = reason;
        ReviewedAt = DateTime.UtcNow;
    }
}
