using FluentAssertions;
using SentientArchitect.Domain.Entities;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.UnitTests.Domain;

public class KnowledgeItemTests
{
    private static readonly Guid UserId   = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public void Constructor_ShouldSetPendingStatus()
    {
        var item = new KnowledgeItem(UserId, TenantId, "title", "content", KnowledgeItemType.Article);

        item.ProcessingStatus.Should().Be(ProcessingStatus.Pending);
    }

    [Fact]
    public void Constructor_ShouldAssignNewId()
    {
        var item = new KnowledgeItem(UserId, TenantId, "title", "content", KnowledgeItemType.Article);

        item.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void MarkAsProcessing_ShouldChangStatusToProcessing()
    {
        var item = new KnowledgeItem(UserId, TenantId, "title", "content", KnowledgeItemType.Article);

        item.MarkAsProcessing();

        item.ProcessingStatus.Should().Be(ProcessingStatus.Processing);
    }

    [Fact]
    public void MarkAsCompleted_ShouldSetSummaryAndCompletedStatus()
    {
        var item = new KnowledgeItem(UserId, TenantId, "title", "content", KnowledgeItemType.Article);

        item.MarkAsCompleted("a summary");

        item.ProcessingStatus.Should().Be(ProcessingStatus.Completed);
        item.Summary.Should().Be("a summary");
    }

    [Fact]
    public void MarkAsFailed_ShouldSetFailedStatus()
    {
        var item = new KnowledgeItem(UserId, TenantId, "title", "content", KnowledgeItemType.Article);

        item.MarkAsFailed();

        item.ProcessingStatus.Should().Be(ProcessingStatus.Failed);
    }

    [Fact]
    public void PublishToShared_ShouldUpdateTenantId()
    {
        var sharedTenantId = Guid.NewGuid();
        var item = new KnowledgeItem(UserId, TenantId, "title", "content", KnowledgeItemType.Article);

        item.PublishToShared(sharedTenantId);

        item.TenantId.Should().Be(sharedTenantId);
    }

    [Fact]
    public void MarkAsProcessing_ShouldUpdateUpdatedAt()
    {
        var item    = new KnowledgeItem(UserId, TenantId, "title", "content", KnowledgeItemType.Article);
        var before  = item.UpdatedAt;

        item.MarkAsProcessing();

        item.UpdatedAt.Should().BeOnOrAfter(before);
    }
}
