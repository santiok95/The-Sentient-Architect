using SentientArchitect.API.Endpoints;
using SentientArchitect.Application.Features.Knowledge.IngestKnowledge;
using SentientArchitect.Application.Features.Knowledge.SearchKnowledge;
using SentientArchitect.Data.Postgres;
using SentientArchitect.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddDataPostgres(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

// Use cases
builder.Services.AddScoped<IngestKnowledgeUseCase>();
builder.Services.AddScoped<SearchKnowledgeUseCase>();

var app = builder.Build();

// Seed roles + admin user
await InfrastructureServiceExtensions.SeedAsync(app.Services);

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// Endpoints
app.MapAuthEndpoints();
app.MapKnowledgeEndpoints();

app.Run();
