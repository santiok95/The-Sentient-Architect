using SentientArchitect.API.Common.Endpoints;
using SentientArchitect.API.Hubs;
using SentientArchitect.API.Middleware;
using SentientArchitect.API.Services;
using SentientArchitect.Application;
using SentientArchitect.Application.Common.Interfaces;
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
builder.Services.AddScoped<IAnalysisProgressReporter, AnalysisProgressReporter>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddAuthorization(options =>
    options.AddPolicy("Admin", policy => policy.RequireRole("Admin")));

var app = builder.Build();

// Seed roles + admin user
await InfrastructureServiceExtensions.SeedAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseCors();
app.UseExceptionHandler();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// SignalR hubs
// TODO: un hub para todos llamado SentientHub o algo 
app.MapHub<ConversationHub>("/hubs/conversation");
app.MapHub<IngestionHub>("/hubs/ingestion");
app.MapHub<AnalysisHub>("/hubs/analysis");

// Endpoints
app.MapEndpointModules();

app.Run();
