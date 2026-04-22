using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SentientArchitect.API.Endpoints;
using SentientArchitect.API.Options;
using SentientArchitect.Application.Common.Interfaces;

namespace SentientArchitect.API.Filters;

/// <summary>
/// Endpoint filter que protege POST /conversations/{id}/chat contra abuso:
///   1. Rechaza mensajes que exceden MaxMessageLength (400).
///   2. Rechaza si el usuario superó DailyTokenBudget (429).
/// Rol Admin bypassa el presupuesto si BypassForAdmin = true.
/// El tracking post-ejecución sigue en ChatExecutionService.TrackTokenUsageAsync;
/// este filter es la puerta que CORTA antes de gastar plata en el LLM.
/// </summary>
internal sealed class ChatMessageLimitsFilter(
    IApplicationDbContext db,
    IUserAccessor userAccessor,
    IHttpContextAccessor httpContextAccessor,
    IOptions<ChatLimitsOptions> options,
    ILogger<ChatMessageLimitsFilter> logger) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext ctx,
        EndpointFilterDelegate next)
    {
        var opts = options.Value;

        var body = ctx.Arguments.OfType<ChatEndpoints.ChatRequest>().FirstOrDefault();
        var messageLength = body?.Message?.Length ?? 0;

        // 1) Length gate
        if (messageLength > opts.MaxMessageLength)
        {
            logger.LogInformation(
                "Chat message rejected — length {Length} exceeds limit {Limit}",
                messageLength, opts.MaxMessageLength);

            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Mensaje demasiado largo.",
                detail: $"Tu mensaje tiene {messageLength.ToString("N0", CultureInfo.InvariantCulture)} caracteres " +
                        $"y el máximo permitido es {opts.MaxMessageLength.ToString("N0", CultureInfo.InvariantCulture)}. " +
                        "Dividilo en partes más cortas o resumí el contexto.");
        }

        // 2) Budget gate
        if (!opts.Enabled)
            return await next(ctx);

        var userId = userAccessor.GetCurrentUserId();
        if (userId == Guid.Empty)
            return await next(ctx);

        if (opts.BypassForAdmin && IsAdmin(httpContextAccessor.HttpContext))
            return await next(ctx);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var tracker = await db.TokenUsageTrackers
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.UserId == userId && t.Date == today);

        if (tracker is not null && tracker.TokensConsumed >= opts.DailyTokenBudget)
        {
            var retryAfter = TimeToNextUtcMidnight();

            logger.LogWarning(
                "Chat blocked by daily token budget. User consumed {Consumed}/{Budget}.",
                tracker.TokensConsumed, opts.DailyTokenBudget);

            return new AuthRateLimitRejectedResult(
                $"Alcanzaste tu límite diario de uso del chat ({opts.DailyTokenBudget.ToString("N0", CultureInfo.InvariantCulture)} tokens). " +
                "Se resetea a las 00:00 UTC. Si necesitás más, pedile a un admin que te amplíe el cupo.",
                retryAfter,
                "chat:daily-budget",
                "user",
                userId.ToString());
        }

        return await next(ctx);
    }

    private static bool IsAdmin(HttpContext? httpContext) =>
        httpContext?.User?.IsInRole("Admin") ?? false;

    private static TimeSpan TimeToNextUtcMidnight()
    {
        var now = DateTime.UtcNow;
        var nextMidnight = now.Date.AddDays(1);
        return nextMidnight - now;
    }
}
