using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;
using SentientArchitect.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace SentientArchitect.Application.Features.Conversations.Chat;

public class ExecuteChatUseCase(
    IApplicationDbContext db,
    SaveMessageUseCase saveMessageUseCase,
    IChatExecutionService chatExecutionService,
    IConversationStreamPublisher streamPublisher,
    IOptions<ConversationOptions> options)
{
    public async Task<Result<ChatExecutionResponse>> ExecuteAsync(
        ExecuteChatRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return await PublishFailureAsync(
                request.ConversationId,
                ["Message is required."],
                ErrorType.Validation,
                ct);

        var conversation = await db.Conversations
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == request.ConversationId && c.UserId == request.UserId, ct);

        if (conversation is null)
            return await PublishFailureAsync(
                request.ConversationId,
                ["Conversation not found."],
                ErrorType.NotFound,
                ct);

        if (request.ActiveRepositoryId.HasValue ||
            request.ContextMode.HasValue ||
            !string.IsNullOrWhiteSpace(request.PreferredStack))
        {
            conversation.UpdateConsultantContext(
                request.ActiveRepositoryId,
                request.PreferredStack,
                request.ContextMode);

            // No separate SaveChangesAsync here — SaveMessageUseCase will persist everything in one shot
        }

        var saveUserMessageResult = await saveMessageUseCase.ExecuteAsync(
            new SaveMessageRequest(request.ConversationId, request.UserId, request.Message, MessageRole.User),
            conversation,
            ct);

        if (!saveUserMessageResult.Succeeded || saveUserMessageResult.Data is null)
            return await PublishFailureAsync(
                request.ConversationId,
                saveUserMessageResult.Errors,
                saveUserMessageResult.ErrorType,
                ct);

        // Count only messages since the last compaction to avoid perpetual re-compaction
        var messagesForThreshold = conversation.LastCompactedAt.HasValue
            ? conversation.Messages.Count(m => m.CreatedAt > conversation.LastCompactedAt.Value)
            : conversation.Messages.Count;

        var shouldCompact = messagesForThreshold >= options.Value.CompactionThreshold;

        var streamedAnyToken = false;
        var executionResult = await chatExecutionService.ExecuteAsync(
            new ChatExecutionRequest(
                request.ConversationId,
                request.Message,
                conversation.AgentType,
                request.ActiveRepositoryId,
                request.PreferredStack,
                request.ContextMode,
                shouldCompact),
            saveUserMessageResult.Data,
            async (token, tokenCt) =>
            {
                streamedAnyToken = true;
                await streamPublisher.PublishTokenAsync(request.ConversationId, token, tokenCt);
            },
            ct);

        if (!executionResult.Succeeded || executionResult.Data is null)
            return await PublishFailureAsync(
                request.ConversationId,
                executionResult.Errors,
                executionResult.ErrorType,
                ct);

        if (!streamedAnyToken && !string.IsNullOrWhiteSpace(executionResult.Data.AssistantMessage))
        {
            await streamPublisher.PublishTokenAsync(
                request.ConversationId,
                executionResult.Data.AssistantMessage,
                ct);
        }

        var saveAssistantMessageResult = await saveMessageUseCase.ExecuteAsync(
            new SaveMessageRequest(
                request.ConversationId,
                request.UserId,
                executionResult.Data.AssistantMessage,
                MessageRole.Assistant),
            conversation,
            ct);

        if (!saveAssistantMessageResult.Succeeded)
            return await PublishFailureAsync(
                request.ConversationId,
                saveAssistantMessageResult.Errors,
                saveAssistantMessageResult.ErrorType,
                ct);

        await streamPublisher.PublishCompleteAsync(request.ConversationId, ct);

        return executionResult;
    }

    private async Task<Result<ChatExecutionResponse>> PublishFailureAsync(
        Guid conversationId,
        List<string> errors,
        ErrorType errorType,
        CancellationToken ct)
    {
        await streamPublisher.PublishErrorAsync(conversationId, string.Join("; ", errors), ct);
        return Result<ChatExecutionResponse>.Failure(errors, errorType);
    }
}
