using FluentAssertions;
using SentientArchitect.Domain.Entities;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.UnitTests.Domain;

public class ConversationTests
{
    private static readonly Guid UserId   = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public void Constructor_ShouldSetActiveStatusAndZeroTokens()
    {
        var conversation = new Conversation(UserId, TenantId);

        conversation.Status.Should().Be(ConversationStatus.Active);
        conversation.TokenCount.Should().Be(0);
    }

    [Fact]
    public void Constructor_ShouldUseDefaultTitle_WhenTitleNotProvided()
    {
        var conversation = new Conversation(UserId, TenantId);

        conversation.Title.Should().Be("Nueva conversación");
    }

    [Fact]
    public void Constructor_ShouldUseProvidedTitle()
    {
        var conversation = new Conversation(UserId, TenantId, "My Chat");

        conversation.Title.Should().Be("My Chat");
    }

    [Fact]
    public void Archive_ShouldSetArchivedStatus()
    {
        var conversation = new Conversation(UserId, TenantId);

        conversation.Archive();

        conversation.Status.Should().Be(ConversationStatus.Archived);
    }

    [Fact]
    public void UpdateSummary_ShouldSetCompactedStatusAndNewTokenCount()
    {
        var conversation = new Conversation(UserId, TenantId);

        conversation.UpdateSummary("summary text", 1500);

        conversation.Status.Should().Be(ConversationStatus.Compacted);
        conversation.Summary.Should().Be("summary text");
        conversation.TokenCount.Should().Be(1500);
    }

    [Fact]
    public void UpdateTitle_ShouldChangeTitle()
    {
        var conversation = new Conversation(UserId, TenantId, "Old Title");

        conversation.UpdateTitle("New Title");

        conversation.Title.Should().Be("New Title");
    }

    [Fact]
    public void AddMessage_ShouldIncreasesMessageCount()
    {
        var conversation = new Conversation(UserId, TenantId);
        var message = new ConversationMessage(conversation.Id, MessageRole.User, "Hello");

        conversation.AddMessage(message);

        conversation.Messages.Should().HaveCount(1);
    }
}
