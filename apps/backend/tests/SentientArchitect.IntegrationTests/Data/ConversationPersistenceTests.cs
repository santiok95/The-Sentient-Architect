using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SentientArchitect.Domain.Entities;
using SentientArchitect.Domain.Enums;
using SentientArchitect.IntegrationTests.Fixtures;
using SentientArchitect.IntegrationTests.Helpers;

namespace SentientArchitect.IntegrationTests.Data;

[Collection("Postgres")]
public class ConversationPersistenceTests(PostgresContainerFixture fixture)
{
    private readonly PostgresContainerFixture _fixture = fixture;

    [Fact]
    public async Task ShouldPersistConversationWithMessages()
    {
        var userId       = await TestDataBuilder.CreateUserAsync(_fixture.Context);
        var tenantId     = userId;
        var conversation = new Conversation(userId, tenantId, "My first chat");
        var message      = new ConversationMessage(conversation.Id, MessageRole.User, "Hello, architect!");

        conversation.AddMessage(message);

        _fixture.Context.Conversations.Add(conversation);
        _fixture.Context.ConversationMessages.Add(message);
        await _fixture.Context.SaveChangesAsync();

        var retrieved = await _fixture.Context.Conversations
            .AsNoTracking()
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == conversation.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Title.Should().Be("My first chat");
        retrieved.Messages.Should().HaveCount(1);
        retrieved.Messages.First().Content.Should().Be("Hello, architect!");
    }

    [Fact]
    public async Task ShouldPersistConversationArchive()
    {
        var userId       = await TestDataBuilder.CreateUserAsync(_fixture.Context);
        var conversation = new Conversation(userId, userId, "Archive Me");
        _fixture.Context.Conversations.Add(conversation);
        await _fixture.Context.SaveChangesAsync();

        conversation.Archive();
        await _fixture.Context.SaveChangesAsync();

        var retrieved = await _fixture.Context.Conversations
            .AsNoTracking()
            .FirstAsync(c => c.Id == conversation.Id);

        retrieved.Status.Should().Be(ConversationStatus.Archived);
    }

    [Fact]
    public async Task ShouldFilterConversationsByUserId()
    {
        var targetUserId = await TestDataBuilder.CreateUserAsync(_fixture.Context);
        var otherUserId  = await TestDataBuilder.CreateUserAsync(_fixture.Context);

        _fixture.Context.Conversations.Add(new Conversation(targetUserId, targetUserId, "User A Chat 1"));
        _fixture.Context.Conversations.Add(new Conversation(targetUserId, targetUserId, "User A Chat 2"));
        _fixture.Context.Conversations.Add(new Conversation(otherUserId, otherUserId, "User B Chat"));
        await _fixture.Context.SaveChangesAsync();

        var userConversations = await _fixture.Context.Conversations
            .AsNoTracking()
            .Where(c => c.UserId == targetUserId)
            .ToListAsync();

        userConversations.Should().HaveCount(2);
        userConversations.Should().AllSatisfy(c => c.UserId.Should().Be(targetUserId));
    }
}
