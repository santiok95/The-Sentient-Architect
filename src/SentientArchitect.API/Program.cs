using SentientArchitect.API.Endpoints;
using SentientArchitect.Application.Features.Admin.ReviewPublishRequest;
using SentientArchitect.Application.Features.Conversations.CreateConversation;
using SentientArchitect.Application.Features.Conversations.GetConversations;
using SentientArchitect.Application.Features.Knowledge.IngestKnowledge;
using SentientArchitect.Application.Features.Knowledge.SearchKnowledge;
using SentientArchitect.Application.Features.Profile.AcceptSuggestion;
using SentientArchitect.Application.Features.Profile.GetProfile;
using SentientArchitect.Application.Features.Profile.RejectSuggestion;
using SentientArchitect.Application.Features.Profile.UpdateProfile;
using SentientArchitect.Application.Features.Repositories.GetAnalysisReport;
using SentientArchitect.Application.Features.Repositories.GetRepositories;
using SentientArchitect.Application.Features.Repositories.SubmitRepository;
using SentientArchitect.Application.Features.Trends.GetTrendSnapshots;
using SentientArchitect.Application.Features.Trends.GetTrends;
using SentientArchitect.Data.Postgres;
using SentientArchitect.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddDataPostgres(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddAuthorization(options =>
    options.AddPolicy("Admin", policy => policy.RequireRole("Admin")));

// Use cases — Knowledge
builder.Services.AddScoped<IngestKnowledgeUseCase>();
builder.Services.AddScoped<SearchKnowledgeUseCase>();

// Use cases — Conversations
builder.Services.AddScoped<CreateConversationUseCase>();
builder.Services.AddScoped<GetConversationsUseCase>();

// Use cases — Profile
builder.Services.AddScoped<GetProfileUseCase>();
builder.Services.AddScoped<UpdateProfileUseCase>();
builder.Services.AddScoped<AcceptSuggestionUseCase>();
builder.Services.AddScoped<RejectSuggestionUseCase>();

// Use cases — Repositories (Code Guardian)
builder.Services.AddScoped<SubmitRepositoryUseCase>();
builder.Services.AddScoped<GetRepositoriesUseCase>();
builder.Services.AddScoped<GetAnalysisReportUseCase>();

// Use cases — Trends Radar
builder.Services.AddScoped<GetTrendsUseCase>();
builder.Services.AddScoped<GetTrendSnapshotsUseCase>();

// Use cases — Admin
builder.Services.AddScoped<ReviewPublishRequestUseCase>();

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
app.MapConversationEndpoints();
app.MapProfileEndpoints();
app.MapRepositoryEndpoints();
app.MapTrendEndpoints();
app.MapAdminEndpoints();

app.Run();
