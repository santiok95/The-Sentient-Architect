using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Application.Features.Conversations.Chat;

public class ExecuteChatUseCase(
    SaveMessageUseCase saveMessageUseCase,
    IChatExecutionService chatExecutionService)
{
    public async Task<Result<ChatExecutionResponse>> ExecuteAsync(
        ExecuteChatRequest request,
        Func<string, CancellationToken, Task>? onToken = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return Result<ChatExecutionResponse>.Failure(["Message is required."], ErrorType.Validation);

        var saveUserMessageResult = await saveMessageUseCase.ExecuteAsync(
            new SaveMessageRequest(request.ConversationId, request.UserId, request.Message, MessageRole.User),
            ct);

        if (!saveUserMessageResult.Succeeded || saveUserMessageResult.Data is null)
            return Result<ChatExecutionResponse>.Failure(
                saveUserMessageResult.Errors,
                saveUserMessageResult.ErrorType);

        var executionResult = await chatExecutionService.ExecuteAsync(
            new ChatExecutionRequest(request.ConversationId, request.Message, request.AgentType),
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
            ct);

        if (!saveAssistantMessageResult.Succeeded)
            return Result<ChatExecutionResponse>.Failure(
                saveAssistantMessageResult.Errors,
                saveAssistantMessageResult.ErrorType);

        return executionResult;
    }
}
