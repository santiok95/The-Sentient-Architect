using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SentientArchitect.Domain.Entities;
using SentientArchitect.Domain.Enums;
using SentientArchitect.IntegrationTests.Fixtures;
using SentientArchitect.IntegrationTests.Helpers;

namespace SentientArchitect.IntegrationTests.Data;

[Collection("Postgres")]
public class KnowledgeItemPersistenceTests(PostgresContainerFixture fixture)
{
    private readonly PostgresContainerFixture _fixture = fixture;

    [Fact]
    public async Task ShouldPersistAndRetrieveKnowledgeItem()
    {
        var userId   = await TestDataBuilder.CreateUserAsync(_fixture.Context);
        var tenantId = userId;
        var item     = new KnowledgeItem(userId, tenantId, "Integration Test Article",
            "Some content about architecture", KnowledgeItemType.Article);

        _fixture.Context.KnowledgeItems.Add(item);
        await _fixture.Context.SaveChangesAsync();

        var retrieved = await _fixture.Context.KnowledgeItems
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.Id == item.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Title.Should().Be("Integration Test Article");
        retrieved.ProcessingStatus.Should().Be(ProcessingStatus.Pending);
    }

    [Fact]
    public async Task ShouldPersistStatusTransitions()
    {
        var userId = await TestDataBuilder.CreateUserAsync(_fixture.Context);
        var item = new KnowledgeItem(userId, userId, "Status Test",
            "Content", KnowledgeItemType.Note);

        _fixture.Context.KnowledgeItems.Add(item);
        await _fixture.Context.SaveChangesAsync();

        item.MarkAsProcessing();
        await _fixture.Context.SaveChangesAsync();

        item.MarkAsCompleted("Summary of content");
        await _fixture.Context.SaveChangesAsync();

        var retrieved = await _fixture.Context.KnowledgeItems
            .AsNoTracking()
            .FirstAsync(k => k.Id == item.Id);

        retrieved.ProcessingStatus.Should().Be(ProcessingStatus.Completed);
        retrieved.Summary.Should().Be("Summary of content");
    }

    [Fact]
    public async Task ShouldStoreEnumAsString()
    {
        var userId = await TestDataBuilder.CreateUserAsync(_fixture.Context);
        var item = new KnowledgeItem(userId, userId, "Enum Test",
            "Content", KnowledgeItemType.RepositoryReference);

        _fixture.Context.KnowledgeItems.Add(item);
        await _fixture.Context.SaveChangesAsync();

        // Query the raw column value to verify string storage
        var connection = _fixture.Context.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT \"Type\" FROM \"KnowledgeItems\" WHERE \"Id\" = '{item.Id}'";
        var rawValue = (string?)await command.ExecuteScalarAsync();
        await connection.CloseAsync();

        rawValue.Should().Be("RepositoryReference");
    }

    [Fact]
    public async Task ShouldDeleteKnowledgeItem()
    {
        var userId = await TestDataBuilder.CreateUserAsync(_fixture.Context);
        var item = new KnowledgeItem(userId, userId, "To Delete",
            "Content", KnowledgeItemType.Article);

        _fixture.Context.KnowledgeItems.Add(item);
        await _fixture.Context.SaveChangesAsync();

        _fixture.Context.KnowledgeItems.Remove(item);
        await _fixture.Context.SaveChangesAsync();

        var exists = await _fixture.Context.KnowledgeItems.AnyAsync(k => k.Id == item.Id);
        exists.Should().BeFalse();
    }
}
