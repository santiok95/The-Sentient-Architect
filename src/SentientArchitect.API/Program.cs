using SentientArchitect.API.Common.Endpoints;
using SentientArchitect.API.Hubs;
using SentientArchitect.Application;
using SentientArchitect.Data.Postgres;
using SentientArchitect.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSignalR();
builder.Services.AddDataPostgres(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

builder.Services.AddAuthorization(options =>
    options.AddPolicy("Admin", policy => policy.RequireRole("Admin")));

var app = builder.Build();

// Seed roles + admin user
await InfrastructureServiceExtensions.SeedAsync(app.Services);

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

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
