using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SentientArchitect.Application.Features.Knowledge.RequestPublishKnowledge;
using SentientArchitect.Domain.Entities;
using SentientArchitect.Domain.Enums;
using SentientArchitect.UnitTests.Common;

namespace SentientArchitect.UnitTests.Application.Features.Knowledge;

public class RequestPublishKnowledgeUseCaseTests : TestBase
{
    private readonly RequestPublishKnowledgeUseCase _sut;

    public RequestPublishKnowledgeUseCaseTests()
    {
        _sut = new RequestPublishKnowledgeUseCase(DbContext);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnSuccess_WhenValidRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var item = new KnowledgeItem(userId, Guid.NewGuid(), "Test Item", "Content", KnowledgeItemType.Article);
        DbContext.KnowledgeItems.Add(item);
        await DbContext.SaveChangesAsync();

        var request = new RequestPublishKnowledgeRequest(userId, item.Id, "Reason");

        // Act
        var result = await _sut.ExecuteAsync(request);

        // Assert
        result.Succeeded.Should().BeTrue();
        var publishRequest = await DbContext.ContentPublishRequests.FirstOrDefaultAsync();
        publishRequest.Should().NotBeNull();
        publishRequest!.KnowledgeItemId.Should().Be(item.Id);
        publishRequest.RequestReason.Should().Be("Reason");
        publishRequest.Status.Should().Be(PublishRequestStatus.Pending);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnForbidden_WhenNotOwner()
    {
        // Arrange
        var ownerUserId = Guid.NewGuid();
        var hackerUserId = Guid.NewGuid();
        var item = new KnowledgeItem(ownerUserId, Guid.NewGuid(), "Test Item", "Content", KnowledgeItemType.Article);
        DbContext.KnowledgeItems.Add(item);
        await DbContext.SaveChangesAsync();

        var request = new RequestPublishKnowledgeRequest(hackerUserId, item.Id);

        // Act
        var result = await _sut.ExecuteAsync(request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("You can only request publishing for your own knowledge items.");
    }
}
