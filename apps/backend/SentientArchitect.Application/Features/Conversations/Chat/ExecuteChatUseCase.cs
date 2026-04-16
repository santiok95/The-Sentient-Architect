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
    IOptions<ConversationOptions> options)
{
    public async Task<Result<ChatExecutionResponse>> ExecuteAsync(
        ExecuteChatRequest request,
        Func<string, CancellationToken, Task>? onToken = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return Result<ChatExecutionResponse>.Failure(["Message is required."], ErrorType.Validation);

        var conversation = await db.Conversations
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == request.ConversationId && c.UserId == request.UserId, ct);

        if (conversation is null)
            return Result<ChatExecutionResponse>.Failure(["Conversation not found."], ErrorType.NotFound);

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
            return Result<ChatExecutionResponse>.Failure(
                saveUserMessageResult.Errors,
                saveUserMessageResult.ErrorType);

        // Count only messages since the last compaction to avoid perpetual re-compaction
        var messagesForThreshold = conversation.LastCompactedAt.HasValue
            ? conversation.Messages.Count(m => m.CreatedAt > conversation.LastCompactedAt.Value)
            : conversation.Messages.Count;

        var shouldCompact = messagesForThreshold >= options.Value.CompactionThreshold;

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
            onToken,
            ct);

        if (!executionResult.Succeeded || executionResult.Data is null)
            return Result<ChatExecutionResponse>.Failure(executionResult.Errors, executionResult.ErrorType);

        var saveAssistantMessageResult = await saveMessageUseCase.ExecuteAsync(
            new SaveMessageRequest(
                request.ConversationId,
                request.UserId,
                executionResult.Data.AssistantMessage,
                MessageRole.Assistant),
            conversation,
            ct);

        if (!saveAssistantMessageResult.Succeeded)
            return Result<ChatExecutionResponse>.Failure(
                saveAssistantMessageResult.Errors,
                saveAssistantMessageResult.ErrorType);

        return executionResult;
    }
}
