using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SentientArchitect.API.Common.Endpoints;
using SentientArchitect.API.Hubs;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Features.Conversations.Chat;
using SentientArchitect.Domain.Entities;
using SentientArchitect.Domain.Enums;
using SentientArchitect.Infrastructure.Agents;
using SentientArchitect.Infrastructure.Agents.Consultant;
using SentientArchitect.Infrastructure.Agents.Knowledge;

namespace SentientArchitect.API.Endpoints;

public class ChatEndpoints : IEndpointModule
{
#pragma warning disable CS0618
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/conversations")
            .WithTags("Chat")
            .RequireAuthorization();

        group.MapPost("/{conversationId:guid}/chat", async (
            [FromRoute] Guid conversationId,
            [FromBody] ChatRequest body,
            [FromServices] IUserAccessor userAccessor,
            [FromServices] SaveMessageUseCase saveMessageUseCase,
            [FromServices] IHubContext<ConversationHub> hubContext,
            [FromServices] KnowledgeAgentFactory knowledgeFactory,
            [FromServices] ConsultantAgentFactory consultantFactory,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var userId = userAccessor.GetCurrentUserId();

            // Save the user message and get context
            var result = await saveMessageUseCase.ExecuteAsync(new SaveMessageRequest(conversationId, userId, body.Message, MessageRole.User), ct);
            
            if (!result.IsSuccess)
                return Results.NotFound(result.Errors);

            var history = new ChatHistory();
            foreach (var msg in result.Data!)
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
                            await hubContext.Clients.Group(groupName).SendAsync("ReceiveToken", chunk.Content, ct);
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
                            await hubContext.Clients.Group(groupName).SendAsync("ReceiveToken", chunk.Content, ct);
                        }
                    }
                }

                // Save the assistant reply
                await saveMessageUseCase.ExecuteAsync(new SaveMessageRequest(conversationId, userId, fullResponse.ToString(), MessageRole.Assistant), ct);

                await hubContext.Clients.Group(groupName).SendAsync("ReceiveComplete", ct);
            }
            catch (Exception ex)
            {
                await hubContext.Clients.Group(groupName).SendAsync("ReceiveError", ex.Message, ct);
                return Results.Problem(ex.Message);
            }

            return Results.Ok();
        })
        .WithName("Chat")
        .WithOpenApi();
    }
#pragma warning restore CS0618

    private record ChatRequest(string Message, string AgentType = "Knowledge");
}
