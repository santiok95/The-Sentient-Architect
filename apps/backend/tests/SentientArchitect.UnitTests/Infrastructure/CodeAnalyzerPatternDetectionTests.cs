using FluentAssertions;
using SentientArchitect.Infrastructure.Guardian;

namespace SentientArchitect.UnitTests.Infrastructure;

/// <summary>
/// Tests the Roslyn-based architectural pattern detection added to CodeAnalyzer.
/// Each test writes C# source to a temp file so the real static analysis runs end-to-end.
/// </summary>
public class CodeAnalyzerPatternDetectionTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"sa-test-{Guid.NewGuid():N}");
    private readonly Guid   _reportId = Guid.NewGuid();

    public CodeAnalyzerPatternDetectionTests()
        => Directory.CreateDirectory(_tempDir);

    public void Dispose()
        => Directory.Delete(_tempDir, recursive: true);

    // ── Repository pattern conflict ───────────────────────────────────────────

    [Fact]
    public async Task Detect_RepositoryConflict_WhenRepoInterfaceAndDbContextCoexist()
    {
        // Both IXxxRepository and direct DbContext injection — conflicting conventions
        var repoFile = WriteCs("IOrderRepository.cs", """
            public interface IOrderRepository
            {
                Task<Order?> GetByIdAsync(Guid id);
            }
            """);
        var useCaseFile = WriteCs("CreateOrderUseCase.cs", """
            public class CreateOrderUseCase(AppDbContext db)
            {
                public void Handle() { }
            }
            """);

        var findings = await CodeAnalyzer.DetectArchitecturalPatternsAsync(
            [repoFile, useCaseFile], _tempDir, _reportId, default);

        findings.Should().Contain(f =>
            f.Category == "Arquitectura" && f.Message.Contains("inconsistente"));
    }

    [Fact]
    public async Task NoFinding_WhenOnlyRepositoryInterfaceWithNoDbContextConflict()
    {
        // Repository-only project — no conflict, no noise
        var file = WriteCs("IOrderRepository.cs", """
            public interface IOrderRepository
            {
                Task<Order?> GetByIdAsync(Guid id);
            }
            """);

        var findings = await CodeAnalyzer.DetectArchitecturalPatternsAsync([file], _tempDir, _reportId, default);

        findings.Should().NotContain(f => f.Category == "Arquitectura" && f.Message.Contains("inconsistente"));
    }

    // ── API style ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Detect_MixedApiStyle_WhenBothMinimalApiAndControllersExist()
    {
        var minimalFile = WriteCs("OrderEndpoints.cs", """
            public static class OrderEndpoints
            {
                public static void Map(WebApplication app)
                    => app.MapGet("/orders", () => Results.Ok());
            }
            """);
        var controllerFile = WriteCs("ProductsController.cs", """
            [ApiController]
            public class ProductsController : ControllerBase
            {
                public IActionResult Get() => Ok();
            }
            """);

        var findings = await CodeAnalyzer.DetectArchitecturalPatternsAsync(
            [minimalFile, controllerFile], _tempDir, _reportId, default);

        findings.Should().Contain(f =>
            f.Category == "Arquitectura" && f.Message.Contains("mixto"));
    }

    [Fact]
    public async Task NoFinding_WhenOnlyMinimalApiIsUsed()
    {
        var file = WriteCs("Endpoints.cs", """
            public static class OrderEndpoints
            {
                public static void Map(WebApplication app)
                {
                    app.MapGet("/orders", () => Results.Ok());
                    app.MapPost("/orders", () => Results.Ok());
                }
            }
            """);

        var findings = await CodeAnalyzer.DetectArchitecturalPatternsAsync([file], _tempDir, _reportId, default);

        // Pure Minimal API is fine — no noise
        findings.Should().NotContain(f => f.Category == "Arquitectura" && f.Message.Contains("mixto"));
    }

    // ── Vertical Slice / Feature folder ──────────────────────────────────────

    [Fact]
    public async Task Detect_CleanArchitecture_WhenFilesSpanMultipleLayers()
    {
        var domainFile = WriteCs("Domain/Entity.cs",         "public class Order { }");
        var appFile    = WriteCs("Application/UseCase.cs",   "public class CreateOrder { }");
        var infraFile  = WriteCs("Infrastructure/Service.cs","public class EmailService { }");

        // No finding expected — Clean Architecture is fine, no noise
        var findings = await CodeAnalyzer.DetectArchitecturalPatternsAsync(
            [domainFile, appFile, infraFile], _tempDir, _reportId, default);

        findings.Should().NotContain(f => f.Category == "Arquitectura" && f.Severity == SentientArchitect.Domain.Enums.FindingSeverity.Medium);
    }

    // ── No false positives ────────────────────────────────────────────────────

    [Fact]
    public async Task DoesNotFlag_RepositoryInfo_AsRepositoryPattern()
    {
        var file = WriteCs("RepositoryInfo.cs", """
            public class RepositoryInfo
            {
                public string Url { get; set; } = string.Empty;
            }
            """);

        var findings = await CodeAnalyzer.DetectArchitecturalPatternsAsync([file], _tempDir, _reportId, default);

        findings.Should().NotContain(f => f.Message.Contains("inconsistente"));
    }

    // ── Helper ───────────────────────────────────────────────────────────────

    private string WriteCs(string relativePath, string content)
    {
        var fullPath = Path.Combine(_tempDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }
}
