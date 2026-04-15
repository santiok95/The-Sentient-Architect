using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Features.Knowledge.IngestKnowledge;
using SentientArchitect.Domain.Entities;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.UnitTests.Application;

public class IngestKnowledgeUseCaseTests
{
    private readonly IApplicationDbContext _db;
    private readonly IVectorStore         _vectorStore;
    private readonly IEmbeddingService    _embeddingService;
    private readonly IngestKnowledgeUseCase _sut;

    public IngestKnowledgeUseCaseTests()
    {
        _db               = Substitute.For<IApplicationDbContext>();
        _vectorStore      = Substitute.For<IVectorStore>();
        _embeddingService = Substitute.For<IEmbeddingService>();

        // Pre-create substitutes to avoid NSubstitute nested-call detection
        var knowledgeItemsDbSet = CreateDbSetSubstitute<KnowledgeItem>();
        var tagsDbSet           = CreateDbSetSubstitute<Tag>();
        var kiTagsDbSet         = CreateDbSetSubstitute<KnowledgeItemTag>();

        _db.KnowledgeItems.Returns(knowledgeItemsDbSet);
        _db.Tags.Returns(tagsDbSet);
        _db.KnowledgeItemTags.Returns(kiTagsDbSet);
        _db.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        _embeddingService
            .GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new float[1536]);

        _sut = new IngestKnowledgeUseCase(_db, _vectorStore, _embeddingService);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFailure_WhenTitleIsEmpty()
    {
        var request = new IngestKnowledgeRequest(
            Guid.NewGuid(), Guid.NewGuid(), "", "content", KnowledgeItemType.Article);

        var result = await _sut.ExecuteAsync(request);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Contain("Title is required");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFailure_WhenContentIsEmpty()
    {
        var request = new IngestKnowledgeRequest(
            Guid.NewGuid(), Guid.NewGuid(), "My Title", "", KnowledgeItemType.Article);

        var result = await _sut.ExecuteAsync(request);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Contain("Content is required");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnBothErrors_WhenTitleAndContentAreEmpty()
    {
        var request = new IngestKnowledgeRequest(
            Guid.NewGuid(), Guid.NewGuid(), "", "", KnowledgeItemType.Article);

        var result = await _sut.ExecuteAsync(request);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPersistItem_WhenRequestIsValid()
    {
        var request = new IngestKnowledgeRequest(
            Guid.NewGuid(), Guid.NewGuid(), "Valid Title", "Valid content body", KnowledgeItemType.Article);

        var result = await _sut.ExecuteAsync(request);

        result.Succeeded.Should().BeTrue();
        _db.KnowledgeItems.Received(1).Add(Arg.Any<KnowledgeItem>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldGenerateEmbedding_ForEachChunk()
    {
        var content = new string('x', 1000); // 2 chunks of 800 with 50 overlap
        var request = new IngestKnowledgeRequest(
            Guid.NewGuid(), Guid.NewGuid(), "Title", content, KnowledgeItemType.Article);

        await _sut.ExecuteAsync(request);

        await _embeddingService.Received(2)
            .GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnChunkCount_OnSuccess()
    {
        // chunkSize=800, overlap=50 → step=750
        // content of 500 chars → 1 chunk (fits fully in first window)
        var content = new string('x', 500);
        var request = new IngestKnowledgeRequest(
            Guid.NewGuid(), Guid.NewGuid(), "Title", content, KnowledgeItemType.Article);

        var result = await _sut.ExecuteAsync(request);

        result.Succeeded.Should().BeTrue();
        result.Data!.ChunksCreated.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMarkItemFailed_WhenEmbeddingServiceThrows()
    {
        _embeddingService
            .GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<float[]>(_ => throw new InvalidOperationException("embedding error"));

        var request = new IngestKnowledgeRequest(
            Guid.NewGuid(), Guid.NewGuid(), "Title", "content", KnowledgeItemType.Article);

        var result = await _sut.ExecuteAsync(request);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Contain("Ingestion failed");
    }

    // Helper: creates a substitutable DbSet backed by an in-memory list
    private static DbSet<T> CreateDbSetSubstitute<T>() where T : class
    {
        var data      = new List<T>().AsQueryable();
        var dbSet     = Substitute.For<DbSet<T>, IQueryable<T>>();

        ((IQueryable<T>)dbSet).Provider.Returns(data.Provider);
        ((IQueryable<T>)dbSet).Expression.Returns(data.Expression);
        ((IQueryable<T>)dbSet).ElementType.Returns(data.ElementType);
        ((IQueryable<T>)dbSet).GetEnumerator().Returns(data.GetEnumerator());

        return dbSet;
    }
}
