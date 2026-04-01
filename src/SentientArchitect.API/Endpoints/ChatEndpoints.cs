using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SentientArchitect.API.Hubs;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Domain.Entities;
using SentientArchitect.Domain.Enums;
using SentientArchitect.Infrastructure.Agents;
using SentientArchitect.Infrastructure.Agents.Consultant;
using SentientArchitect.Infrastructure.Agents.Knowledge;

namespace SentientArchitect.API.Endpoints;

public static class ChatEndpoints
{
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/conversations")
            .WithTags("Chat")
            .RequireAuthorization();

        group.MapPost("/{conversationId:guid}/chat", ChatAsync)
            .WithName("Chat")
            .WithOpenApi();

        return app;
    }

#pragma warning disable CS0618 // InvokeStreamingAsync(ChatHistory) is obsolete but AgentThread API is still experimental
    private static async Task<IResult> ChatAsync(
        Guid conversationId,
        ChatRequest body,
        IUserAccessor userAccessor,
        IApplicationDbContext db,
        IHubContext<ConversationHub> hubContext,
        KnowledgeAgentFactory knowledgeFactory,
        ConsultantAgentFactory consultantFactory,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var userId = userAccessor.GetCurrentUserId();

        // Load conversation with messages for tracking
        var conversation = await db.Conversations
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == conversationId, ct);

        if (conversation is null || conversation.UserId != userId)
            return Results.NotFound();

        // Save the user message
        var userMessage = new ConversationMessage(conversationId, MessageRole.User, body.Message);
        conversation.AddMessage(userMessage);
        await db.SaveChangesAsync(ct);

        // Build ChatHistory from last 20 messages (oldest first, including the one just saved)
        var history = new ChatHistory();
        var recentMessages = conversation.Messages
            .OrderBy(m => m.CreatedAt)
            .TakeLast(20)
            .ToList();

        foreach (var msg in recentMessages)
        {
            var role = msg.Role switch
            {
                MessageRole.User      => AuthorRole.User,
                MessageRole.Assistant => AuthorRole.Assistant,
                MessageRole.System    => AuthorRole.System,
                _                     => AuthorRole.User,
            };
            history.Add(new ChatMessageContent(role, msg.Content));
        }

        // Resolve scoped plugins from the request's DI scope
        var services = httpContext.RequestServices;
        var groupName = conversationId.ToString();
        var fullResponse = new System.Text.StringBuilder();

        try
        {
            if (string.Equals(body.AgentType, "Consultant", StringComparison.OrdinalIgnoreCase))
            {
                var profilePlugin = services.GetRequiredService<ProfilePlugin>();
                var summaryPlugin = services.GetRequiredService<SummaryPlugin>();
                var searchPlugin  = services.GetRequiredService<SearchPlugin>();
                var agent = consultantFactory.Create(profilePlugin, summaryPlugin, searchPlugin);

                await foreach (var chunk in agent.InvokeStreamingAsync(history, cancellationToken: ct))
                {
                    if (!string.IsNullOrEmpty(chunk.Content))
                    {
                        fullResponse.Append(chunk.Content);
                        await hubContext.Clients.Group(groupName)
                            .SendAsync("ReceiveToken", chunk.Content, ct);
                    }
                }
            }
            else
            {
                var searchPlugin = services.GetRequiredService<SearchPlugin>();
                var ingestPlugin = services.GetRequiredService<IngestPlugin>();
                var agent = knowledgeFactory.Create(searchPlugin, ingestPlugin);

                await foreach (var chunk in agent.InvokeStreamingAsync(history, cancellationToken: ct))
                {
                    if (!string.IsNullOrEmpty(chunk.Content))
                    {
                        fullResponse.Append(chunk.Content);
                        await hubContext.Clients.Group(groupName)
                            .SendAsync("ReceiveToken", chunk.Content, ct);
                    }
                }
            }

            // Save the assistant reply
            var assistantMessage = new ConversationMessage(
                conversationId,
                MessageRole.Assistant,
                fullResponse.ToString());

            conversation.AddMessage(assistantMessage);

            await hubContext.Clients.Group(groupName).SendAsync("ReceiveComplete", ct);
        }
        catch (Exception ex)
        {
            await hubContext.Clients.Group(groupName)
                .SendAsync("ReceiveError", ex.Message, ct);
            return Results.Problem(ex.Message);
        }

        await db.SaveChangesAsync(ct);

        return Results.Ok();
    }
#pragma warning restore CS0618

    private record ChatRequest(string Message, string AgentType = "Knowledge");
}
