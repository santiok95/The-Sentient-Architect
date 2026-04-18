using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;
using SentientArchitect.Application.Features.Conversations.Chat;
using SentientArchitect.Domain.Entities;
using SentientArchitect.Domain.Enums;
using SentientArchitect.UnitTests.Common;

namespace SentientArchitect.UnitTests.Application.Features.Conversations;

public class ExecuteChatUseCaseTests : TestBase
{
    private readonly IChatExecutionService _chatExecutionService = Substitute.For<IChatExecutionService>();
    private readonly IConversationStreamPublisher _streamPublisher = Substitute.For<IConversationStreamPublisher>();
    private readonly SaveMessageUseCase _saveMessageUseCase;
    private readonly ExecuteChatUseCase _sut;

    public ExecuteChatUseCaseTests()
    {
        _saveMessageUseCase = new SaveMessageUseCase(DbContext);
        _sut = new ExecuteChatUseCase(
            DbContext,
            _saveMessageUseCase,
            _chatExecutionService,
            _streamPublisher,
            Options.Create(new ConversationOptions { CompactionThreshold = 20 }));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPublishFallbackTokenAndComplete_WhenChatReturnsFinalMessageWithoutStreamedChunks()
    {
        var conversation = await CreateConversationAsync();

        _chatExecutionService
            .ExecuteAsync(
                Arg.Any<ChatExecutionRequest>(),
                Arg.Any<IReadOnlyList<ConversationMessage>>(),
                Arg.Any<Func<string, CancellationToken, Task>?>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<ChatExecutionResponse>.SuccessWith(
                new ChatExecutionResponse("Respuesta final", AgentType.Consultant)));

        var result = await _sut.ExecuteAsync(
            new ExecuteChatRequest(conversation.Id, conversation.UserId, "Hola"));

        result.Succeeded.Should().BeTrue();
        await _streamPublisher.Received(1)
            .PublishTokenAsync(conversation.Id, "Respuesta final", Arg.Any<CancellationToken>());
        await _streamPublisher.Received(1)
            .PublishCompleteAsync(conversation.Id, Arg.Any<CancellationToken>());
        await _streamPublisher.DidNotReceiveWithAnyArgs()
            .PublishErrorAsync(default, default!, default);

        var messages = await DbContext.ConversationMessages
            .Where(m => m.ConversationId == conversation.Id)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        messages.Should().HaveCount(2);
        messages[0].Role.Should().Be(MessageRole.User);
        messages[1].Role.Should().Be(MessageRole.Assistant);
        messages[1].Content.Should().Be("Respuesta final");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPublishStreamedChunksWithoutFallback_WhenChatStreamsTokens()
    {
        var conversation = await CreateConversationAsync();

        _chatExecutionService
            .ExecuteAsync(
                Arg.Any<ChatExecutionRequest>(),
                Arg.Any<IReadOnlyList<ConversationMessage>>(),
                Arg.Any<Func<string, CancellationToken, Task>?>(),
                Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var onToken = callInfo.ArgAt<Func<string, CancellationToken, Task>?>(2);
                if (onToken is not null)
                {
                    await onToken("Hola ", CancellationToken.None);
                    await onToken("mundo", CancellationToken.None);
                }

                return Result<ChatExecutionResponse>.SuccessWith(
                    new ChatExecutionResponse("Hola mundo", AgentType.Consultant));
            });

        var result = await _sut.ExecuteAsync(
            new ExecuteChatRequest(conversation.Id, conversation.UserId, "Hola"));

        result.Succeeded.Should().BeTrue();
        await _streamPublisher.Received(1)
            .PublishTokenAsync(conversation.Id, "Hola ", Arg.Any<CancellationToken>());
        await _streamPublisher.Received(1)
            .PublishTokenAsync(conversation.Id, "mundo", Arg.Any<CancellationToken>());
        await _streamPublisher.Received(2)
            .PublishTokenAsync(conversation.Id, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _streamPublisher.Received(1)
            .PublishCompleteAsync(conversation.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPublishError_WhenChatExecutionFails()
    {
        var conversation = await CreateConversationAsync();

        _chatExecutionService
            .ExecuteAsync(
                Arg.Any<ChatExecutionRequest>(),
                Arg.Any<IReadOnlyList<ConversationMessage>>(),
                Arg.Any<Func<string, CancellationToken, Task>?>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<ChatExecutionResponse>.Failure(["LLM failed."], ErrorType.Failure));

        var result = await _sut.ExecuteAsync(
            new ExecuteChatRequest(conversation.Id, conversation.UserId, "Hola"));

        result.Succeeded.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Failure);
        await _streamPublisher.Received(1)
            .PublishErrorAsync(conversation.Id, "LLM failed.", Arg.Any<CancellationToken>());
        await _streamPublisher.DidNotReceiveWithAnyArgs()
            .PublishCompleteAsync(default, default);
    }

    private async Task<Conversation> CreateConversationAsync()
    {
        var userId = Guid.NewGuid();
        var conversation = new Conversation(userId, userId, "Chat test", AgentType.Consultant);

        DbContext.Conversations.Add(conversation);
        await DbContext.SaveChangesAsync();

        return conversation;
    }
}