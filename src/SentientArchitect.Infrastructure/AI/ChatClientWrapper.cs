using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Runtime.CompilerServices;

namespace SentientArchitect.Infrastructure.AI;

public sealed class ChatClientWrapper(IChatClient chatClient, string defaultModelId) : IChatCompletionService
{
    public IReadOnlyDictionary<string, object?> Attributes => new Dictionary<string, object?>();

    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        var messages = chatHistory.Select(m => new Microsoft.Extensions.AI.ChatMessage(
            new Microsoft.Extensions.AI.ChatRole(m.Role.ToString()), 
            m.Content)).ToList();

        var options = new ChatOptions { ModelId = defaultModelId };

        var response = await chatClient.GetResponseAsync(messages, options, cancellationToken: cancellationToken);
        
        var responseMessage = response.Messages[0];
        
        return [new ChatMessageContent(
            new Microsoft.SemanticKernel.ChatCompletion.AuthorRole(responseMessage.Role.Value), 
            responseMessage.Text)];
    }

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messages = chatHistory.Select(m => new Microsoft.Extensions.AI.ChatMessage(
            new Microsoft.Extensions.AI.ChatRole(m.Role.ToString()), 
            m.Content)).ToList();

        var options = new ChatOptions { ModelId = defaultModelId };

        await foreach (var update in chatClient.GetStreamingResponseAsync(messages, options, cancellationToken: cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                yield return new StreamingChatMessageContent(
                    new Microsoft.SemanticKernel.ChatCompletion.AuthorRole(update.Role?.Value ?? "assistant"), 
                    update.Text);
            }
        }
    }
}
