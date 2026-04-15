using Microsoft.EntityFrameworkCore;
using SentientArchitect.API.Common.Endpoints;
using SentientArchitect.Data;
using SentientArchitect.API.Hubs;
using SentientArchitect.API.Middleware;
using SentientArchitect.API.Services;
using SentientArchitect.Application;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Features.Conversations.Chat;
using SentientArchitect.Data.Postgres;
using SentientArchitect.Infrastructure;
using Scalar.AspNetCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

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
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});
builder.Services.AddSignalR();
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
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
    await db.Database.MigrateAsync();
}
await InfrastructureServiceExtensions.SeedAsync(app.Services);


    app.MapOpenApi();
    app.MapScalarApiReference();


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
