using Microsoft.AspNetCore.Mvc;
using SentientArchitect.API.Common.Endpoints;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Features.Knowledge.IngestKnowledge;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.API.Endpoints;

public class SeedKnowledgeEndpoints : IEndpointModule
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/seed")
            .WithTags("Seed")
            .RequireAuthorization();

        group.MapPost("/project-rules", async (
            [FromServices] IUserAccessor userAccessor,
            [FromServices] IngestKnowledgeUseCase ingestUseCase,
            CancellationToken ct) =>
        {
            var userId = userAccessor.GetCurrentUserId();

            var rules = new[]
            {
                new { Title = "Result Pattern - Error Handling", Content = "The Result pattern is used for all Application services. Return Result or Result<T> from use cases. Never throw exceptions for business logic errors. Exceptions are only for truly exceptional errors (DB down, out of memory). Map Result to HTTP responses via ToHttpResult() extensions." },
                new { Title = "No Repository Pattern", Content = "DO NOT use the Repository pattern. DbContext already IS a Repository + Unit of Work. Inject IApplicationDbContext directly into use cases. Use EF Core LINQ directly. Use AsNoTracking() on all read-only queries." },
                new { Title = "Entity Rules", Content = "ALL entities MUST inherit BaseEntity (Id + CreatedAt). Rich Domain Model: private setters and behavior methods. Private parameterless constructor for EF Core. NO exceptions in Domain. Collections initialize as new HashSet<T>(). Navigation properties nullable." },
                new { Title = "Data Access - EF Core", Content = "Fluent API only - no data annotations. One configuration file per entity. PostgreSQL-specific configs go in Data.Postgres. JSONB for collections using List<string>. Enums stored as string via .HasConversion<string>().HasMaxLength(50). All DateTime properties stored as UTC. Guid PKs generated client-side." },
                new { Title = "Async/Await Convention", Content = "Async/await throughout entire codebase. Suffix async methods with 'Async'. Every entity includes UserId and TenantId for multi-tenancy." },
                new { Title = "Clean Architecture Layers", Content = "Domain: entities, enums, abstractions. ZERO NuGet packages. NO exceptions. Application: interfaces, Result pattern, Use Cases. Data: ApplicationContext, Entity Configs. Data.Postgres: PostgreSQL-specific, Migrations. Infrastructure: Identity services, AI services, background jobs. API: Minimal API endpoints, middleware." },
                new { Title = "Domain Layer - IEntity", Content = "Entities inherit BaseEntity which provides Id and CreatedAt. Implement IEntity interface. Use private setters and behavior methods for state changes. Factory methods NOT used - use constructors directly." },
                new { Title = "Authentication & Authorization", Content = "ASP.NET Identity with JWT bearer tokens. User POCO in Domain (no IdentityUser dependency). ApplicationUser : IdentityUser<Guid> in Data. Two roles: Admin (full access, content review) and User (personal ingestion, queries). Content scope: Personal (TenantId = userId) + Shared (TenantId = org tenantId)." },
                new { Title = "Semantic Kernel Agents", Content = "One Kernel instance per agent. Register AI service via AddOpenAIChatCompletion() or AddAzureOpenAIChatCompletion(). Register plugins via Kernel.Plugins.Add(). Plugins are plain C# classes with [KernelFunction] methods. Use [Description] on methods AND parameters. FunctionChoiceBehavior.Auto() lets LLM decide when to call plugins." },
                new { Title = "Vector DB - pgvector", Content = "PostgreSQL 16+ with pgvector extension. Store embeddings in KnowledgeEmbedding entity. Chunk content 500-800 tokens with ~50 token overlap. Use IVectorStore interface for all operations. Cosine distance for similarity. HNSW index for ANNS. All embeddings MUST use same embedding model." },
            };

            var ingestedCount = 0;
            foreach (var rule in rules)
            {
                var request = new IngestKnowledgeRequest(
                    userId,
                    userId,
                    rule.Title,
                    rule.Content,
                    KnowledgeItemType.Documentation,
                    $"https://project-rules/{rule.Title.ToLowerInvariant().Replace(" ", "-")}",
                    new List<string> { "architecture", "rules", "standards" });

                var result = await ingestUseCase.ExecuteAsync(request, ct);
                if (result.Succeeded)
                    ingestedCount++;
            }

            return Results.Ok(new { message = $"Seeded {ingestedCount} project rules into knowledge base" });
        })
        .WithName("SeedProjectRules")
        .WithOpenApi();
    }
}
