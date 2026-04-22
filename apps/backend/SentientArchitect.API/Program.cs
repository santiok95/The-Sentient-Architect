using System.Net;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using SentientArchitect.API.Common.Endpoints;
using SentientArchitect.API.Hubs;
using SentientArchitect.API.Middleware;
using SentientArchitect.API.Options;
using SentientArchitect.API.Services;
using SentientArchitect.Application;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Features.Conversations.Chat;
using SentientArchitect.Data;
using SentientArchitect.Data.Postgres;
using SentientArchitect.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var configuredCorsOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

var defaultCorsOrigins = new List<string>
{
    "https://ai-lab.santiagoniveyro.online"
};

if (!builder.Environment.IsProduction())
{
    defaultCorsOrigins.AddRange(
    [
        "http://localhost:3000",
        "http://127.0.0.1:3000",
        "http://localhost:3001",
        "http://127.0.0.1:3001"
    ]);
}

var allowedCorsOrigins = configuredCorsOrigins
    .Concat(defaultCorsOrigins)
    .Select(origin => origin.Trim().TrimEnd('/'))
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

// ── ForwardedHeaders — must be configured before anything reads IP ────────
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Trust only the immediate upstream proxy (adjust KnownProxies/KnownNetworks for multi-hop setups)
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
    options.KnownProxies.Add(IPAddress.Loopback);
    options.KnownProxies.Add(IPAddress.IPv6Loopback);
});

builder.Services.AddOptions<AuthRateLimitOptions>()
    .Bind(builder.Configuration.GetSection(AuthRateLimitOptions.SectionName))
    .ValidateDataAnnotations();

builder.Services.AddOptions<ChatRateLimitOptions>()
    .Bind(builder.Configuration.GetSection(ChatRateLimitOptions.SectionName))
    .ValidateDataAnnotations();

builder.Services.AddOptions<ChatLimitsOptions>()
    .Bind(builder.Configuration.GetSection(ChatLimitsOptions.SectionName))
    .ValidateDataAnnotations();

builder.Services.AddHttpContextAccessor();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((doc, _, _) =>
    {
        doc.Components ??= new();
        doc.Components.SecuritySchemes["Bearer"] = new()
        {
            Type     = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme   = "bearer",
            BearerFormat = "JWT",
            Description  = "Paste your JWT token here (without 'Bearer ' prefix)",
        };
        doc.SecurityRequirements.Add(new()
        {
            [new() { Reference = new() { Id = "Bearer", Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme } }] = []
        });
        return Task.CompletedTask;
    });
});
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedCorsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});
builder.Services.AddSignalR();

// ── Rate limiting — auth paths are partitioned by endpoint + client IP ────
var rateLimitOpts = builder.Configuration
    .GetSection(AuthRateLimitOptions.SectionName)
    .Get<AuthRateLimitOptions>() ?? new AuthRateLimitOptions();

builder.Services.AddRateLimiter(limiter =>
{
    limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    limiter.OnRejected = async (ctx, ct) =>
    {
        var retryAfter = ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var leaseRetryAfter)
            ? leaseRetryAfter
            : TimeSpan.FromSeconds(60);

        await new SentientArchitect.API.Filters.AuthRateLimitRejectedResult(
            "Demasiadas solicitudes. Esperá un momento antes de reintentar.",
            retryAfter,
            ResolveAuthRateLimitPolicy(ctx.HttpContext),
            "ip",
            GetClientIpPartitionKey(ctx.HttpContext))
            .ExecuteAsync(ctx.HttpContext);
    };

    if (rateLimitOpts.Enabled)
    {
        limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        {
            var policy = ResolveAuthRateLimitPolicy(httpContext);
            var partitionKey = $"{policy}:{GetClientIpPartitionKey(httpContext)}";

            return policy switch
            {
                RateLimitPolicies.LoginByIp => RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey,
                    _ => BuildSlidingWindowOptions(
                        rateLimitOpts.Login.PermitLimit,
                        rateLimitOpts.Login.WindowSeconds)),

                RateLimitPolicies.RegisterByIp => RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey,
                    _ => BuildSlidingWindowOptions(
                        rateLimitOpts.Register.PermitLimit,
                        rateLimitOpts.Register.WindowSeconds)),

                RateLimitPolicies.RefreshByIp => RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey,
                    _ => BuildSlidingWindowOptions(
                        rateLimitOpts.Refresh.PermitLimit,
                        rateLimitOpts.Refresh.WindowSeconds)),

                _ => RateLimitPartition.GetNoLimiter("auth:no-limit"),
            };
        });
    }
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddDataPostgres(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
builder.Services.Configure<ConversationOptions>(
    builder.Configuration.GetSection(ConversationOptions.SectionName));
builder.Services.AddScoped<IAnalysisProgressReporter, AnalysisProgressReporter>();
builder.Services.AddScoped<IConversationStreamPublisher, SignalRConversationStreamPublisher>();

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<SentientArchitect.API.Filters.ILoginAttemptTracker,
                              SentientArchitect.API.Filters.InMemoryLoginAttemptTracker>();
builder.Services.AddSingleton<SentientArchitect.API.Filters.IUserChatThrottleService,
                              SentientArchitect.API.Filters.InMemoryUserChatThrottleService>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddAuthorization(options =>
    options.AddPolicy("Admin", policy => policy.RequireRole("Admin")));

var app = builder.Build();

// ── Startup validation — fail fast on missing critical config ─────────────
var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
var cfg = app.Configuration;

var missingKeys = new List<string>();
if (string.IsNullOrWhiteSpace(cfg["AI:OpenAI:ApiKey"]))
    missingKeys.Add("AI:OpenAI:ApiKey");
if (string.IsNullOrWhiteSpace(cfg["AI:Anthropic:ApiKey"]))
    missingKeys.Add("AI:Anthropic:ApiKey");
if (string.IsNullOrWhiteSpace(cfg["Jwt:Key"]))
    missingKeys.Add("Jwt:Key");

if (missingKeys.Count > 0)
{
    var missing = string.Join(", ", missingKeys);
    if (app.Environment.IsProduction())
    {
        startupLogger.LogCritical(
            "Startup aborted — missing required configuration keys: {Keys}. " +
            "Set them as environment variables or in appsettings.", missing);
        return;
    }

    startupLogger.LogWarning(
        "Missing configuration keys (non-fatal in Development): {Keys}. " +
        "Embedding and/or AI features will not work.", missing);
}

// CORS origin validation — en prod fallamos si no hay al menos un origen desde config.
startupLogger.LogInformation(
    "CORS allowed origins ({Count}): {Origins}",
    allowedCorsOrigins.Length,
    string.Join(", ", allowedCorsOrigins));

if (app.Environment.IsProduction() && configuredCorsOrigins.Length == 0)
{
    startupLogger.LogCritical(
        "Startup aborted — Cors:AllowedOrigins is empty in production. " +
        "Set Cors__AllowedOrigins__0=https://your-domain as an environment variable.");
    return;
}

// Apply pending migrations and seed roles + admin user
var skipDatabaseInitialization = app.Configuration.GetValue<bool>("Startup:SkipDatabaseInitialization");

if (!skipDatabaseInitialization)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
    await db.Database.MigrateAsync();

    await InfrastructureServiceExtensions.SeedAsync(app.Services);
}


app.MapOpenApi();
app.MapScalarApiReference();

// UseForwardedHeaders MUST run before anything that reads IP (CORS, rate limiting, auth)
app.UseForwardedHeaders();

app.UseRouting();
app.UseRateLimiter();
app.UseCors();
app.UseExceptionHandler();

// Only redirect to HTTPS in Development — in Docker we run plain HTTP behind a reverse proxy
if (app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// SignalR hubs
app.MapHub<ConversationHub>("/hubs/conversation");
app.MapHub<IngestionHub>("/hubs/ingestion");
app.MapHub<AnalysisHub>("/hubs/analysis");

// Endpoints
app.MapEndpointModules();

app.Run();

static SlidingWindowRateLimiterOptions BuildSlidingWindowOptions(int permitLimit, int windowSeconds)
{
    return new SlidingWindowRateLimiterOptions
    {
        PermitLimit = permitLimit,
        Window = TimeSpan.FromSeconds(windowSeconds),
        SegmentsPerWindow = 5,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        QueueLimit = 0,
        AutoReplenishment = true,
    };
}

static string GetClientIpPartitionKey(HttpContext httpContext)
{
    // Cloudflare inyecta CF-Connecting-IP con la IP real del cliente
    // Este header no puede ser falsificado porque Cloudflare lo controla
    if (httpContext.Request.Headers.TryGetValue("CF-Connecting-IP", out var cfIp))
    {
        var ip = cfIp.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(ip))
            return ip;
    }

    // Fallback: X-Forwarded-For (para desarrollo sin Cloudflare)
    if (httpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
    {
        var candidate = forwardedFor.ToString()
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(candidate))
            return candidate;
    }

    // Último fallback: RemoteIpAddress (desarrollo local directo)
    var remoteIp = httpContext.Connection.RemoteIpAddress;
    return remoteIp?.ToString() ?? "unknown";
}

static string ResolveAuthRateLimitPolicy(HttpContext httpContext)
{
    var path = httpContext.Request.Path;

    if (path.StartsWithSegments("/api/v1/auth/login"))
        return RateLimitPolicies.LoginByIp;

    if (path.StartsWithSegments("/api/v1/auth/register"))
        return RateLimitPolicies.RegisterByIp;

    if (path.StartsWithSegments("/api/v1/auth/refresh"))
        return RateLimitPolicies.RefreshByIp;

    return "auth:unknown";
}
