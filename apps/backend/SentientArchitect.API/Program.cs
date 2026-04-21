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
            "Rate limit exceeded. Please wait before retrying.",
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

// ── Temporary diagnostic endpoint ─────────────────────────────────────────
app.MapGet("/debug/headers", (HttpContext ctx) =>
{
    var headers = ctx.Request.Headers
        .Select(h => new { h.Key, Value = h.Value.ToString() })
        .OrderBy(h => h.Key);

    var remoteIp = ctx.Connection.RemoteIpAddress?.ToString();
    var forwardedFor = ctx.Request.Headers["X-Forwarded-For"].ToString();
    var cfConnectingIp = ctx.Request.Headers["CF-Connecting-IP"].ToString();

    return Results.Ok(new
    {
        RemoteIpAddress = remoteIp,
        XForwardedFor = forwardedFor,
        CFConnectingIP = cfConnectingIp,
        AllHeaders = headers
    });
});

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
    var remoteIp = httpContext.Connection.RemoteIpAddress;
    if (remoteIp is not null && !IPAddress.IsLoopback(remoteIp))
        return remoteIp.ToString();

    if (httpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
    {
        var candidate = forwardedFor.ToString()
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(candidate))
            return candidate;
    }

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
