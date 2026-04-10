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

    // ── Direct DbContext injection ─────────────────────────────────────────────

    [Fact]
    public async Task Detect_DirectDbContextInjection_WhenConstructorTakesApplicationDbContext()
    {
        var file = WriteCs("UseCase.cs", """
            public class MyUseCase(IApplicationDbContext db)
            {
                public void Do() => db.SaveChangesAsync(default);
            }
            """);

        var findings = await CodeAnalyzer.DetectArchitecturalPatternsAsync([file], _reportId, default);

        findings.Should().Contain(f =>
            f.Category == "Architecture" &&
            f.Message.Contains("DbContext") &&
            f.Message.Contains("do NOT recommend"));
    }

    [Fact]
    public async Task Detect_DirectDbContextInjection_WhenConstructorTakesXxxContext()
    {
        var file = WriteCs("Handler.cs", """
            public class OrderHandler(AppDbContext context)
            {
                public void Handle() { }
            }
            """);

        var findings = await CodeAnalyzer.DetectArchitecturalPatternsAsync([file], _reportId, default);

        findings.Should().Contain(f =>
            f.Category == "Architecture" && f.Message.Contains("DbContext"));
    }

    // ── Repository pattern ────────────────────────────────────────────────────

    [Fact]
    public async Task Detect_RepositoryInterface_WhenInterfaceNameMatchesIXxxRepository()
    {
        var file = WriteCs("IOrderRepository.cs", """
            public interface IOrderRepository
            {
                Task<Order?> GetByIdAsync(Guid id);
            }
            """);

        var findings = await CodeAnalyzer.DetectArchitecturalPatternsAsync([file], _reportId, default);

        findings.Should().Contain(f =>
            f.Category == "Architecture" && f.Message.Contains("Repository pattern detected"));
    }

    [Fact]
    public async Task Detect_RepositoryClass_WhenClassNameEndsWithRepository()
    {
        var file = WriteCs("OrderRepository.cs", """
            public class OrderRepository : IOrderRepository
            {
                public Task<Order?> GetByIdAsync(Guid id) => Task.FromResult<Order?>(null);
            }
            """);

        var findings = await CodeAnalyzer.DetectArchitecturalPatternsAsync([file], _reportId, default);

        findings.Should().Contain(f =>
            f.Category == "Architecture" && f.Message.Contains("Repository class"));
    }

    // ── Minimal API ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Detect_MinimalApi_WhenMapGetIsUsed()
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

        var findings = await CodeAnalyzer.DetectArchitecturalPatternsAsync([file], _reportId, default);

        findings.Should().Contain(f =>
            f.Category == "Architecture" && f.Message.Contains("Minimal API"));
    }

    // ── Vertical Slice / Feature folder ──────────────────────────────────────

    [Fact]
    public async Task Detect_VerticalSlice_WhenFileIsUnderFeaturesFolder()
    {
        var featuresDir = Path.Combine(_tempDir, "Features", "Orders");
        Directory.CreateDirectory(featuresDir);
        var file = Path.Combine(featuresDir, "CreateOrderUseCase.cs");
        await File.WriteAllTextAsync(file, "public class CreateOrderUseCase { }");

        var findings = await CodeAnalyzer.DetectArchitecturalPatternsAsync([file], _reportId, default);

        findings.Should().Contain(f =>
            f.Category == "Architecture" && f.Message.Contains("Vertical Slice"));
    }

    // ── Clean Architecture layers ─────────────────────────────────────────────

    [Fact]
    public async Task Detect_CleanArchitecture_WhenFilesSpanMultipleLayers()
    {
        var domainFile = WriteCs("Domain/Entity.cs",      "public class Order { }");
        var appFile    = WriteCs("Application/UseCase.cs", "public class CreateOrder { }");
        var infraFile  = WriteCs("Infrastructure/Service.cs", "public class EmailService { }");

        var findings = await CodeAnalyzer.DetectArchitecturalPatternsAsync(
            [domainFile, appFile, infraFile], _reportId, default);

        findings.Should().Contain(f =>
            f.Category == "Architecture" && f.Message.Contains("Clean Architecture"));
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

        var findings = await CodeAnalyzer.DetectArchitecturalPatternsAsync([file], _reportId, default);

        // RepositoryInfo is a domain entity — not the repository pattern
        findings.Should().NotContain(f => f.Message.Contains("Repository class implementations"));
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
