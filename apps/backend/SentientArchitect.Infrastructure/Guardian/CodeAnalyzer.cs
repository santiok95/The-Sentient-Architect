using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Domain.Entities;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Infrastructure.Guardian;

public sealed class CodeAnalyzer(
    IApplicationDbContext db,
    IAnalysisProgressReporter reporter,
    ILogger<CodeAnalyzer> logger) : ICodeAnalyzer
{
    public async Task AnalyzeAsync(Guid repositoryInfoId, CancellationToken ct = default)
    {
        AnalysisReport? report = null;
        string? clonePath = null;

        try
        {
            var repositoryInfo = await db.Repositories
                .FirstOrDefaultAsync(r => r.Id == repositoryInfoId, ct);

            if (repositoryInfo is null)
            {
                logger.LogWarning("RepositoryInfo {RepositoryInfoId} not found — skipping analysis.", repositoryInfoId);
                return;
            }

            // Create report
            report = new AnalysisReport(repositoryInfoId);
            db.AnalysisReports.Add(report);
            await db.SaveChangesAsync(ct);

            report.MarkInProgress();
            await db.SaveChangesAsync(ct);

            await reporter.ReportProgressAsync(repositoryInfoId, 5, "Iniciando análisis...", ct);

            // Clone repository
            // NOTE: Do NOT use Path.GetTempPath() ("/tmp" on Linux) — the path would match
            // the "/tmp/" exclusion segment in IsAnalyzableFile() and zero files would be scanned.
            var cloneBase = Path.Combine(AppContext.BaseDirectory, "sentient-repos");
            clonePath = Path.Combine(cloneBase, repositoryInfoId.ToString());
            if (Directory.Exists(clonePath))
                DeleteDirectory(clonePath);

            LibGit2Sharp.Repository.Clone(repositoryInfo.RepositoryUrl, clonePath);

            repositoryInfo.SetLocalPath(clonePath);
            await db.SaveChangesAsync(ct);

            await reporter.ReportProgressAsync(repositoryInfoId, 15, "Repositorio clonado", ct);

            // Extract git metadata
            using (var gitRepo = new LibGit2Sharp.Repository(clonePath))
            {
                var defaultBranch = gitRepo.Head.FriendlyName;
                var lastCommit = gitRepo.Commits.FirstOrDefault()?.Author.When.UtcDateTime;
                var contributorCount = gitRepo.Commits
                    .Select(c => c.Author.Email)
                    .Distinct()
                    .Count();

                repositoryInfo.UpdateGitMetadata(defaultBranch, null, contributorCount, lastCommit);
                await db.SaveChangesAsync(ct);
            }

            await reporter.ReportProgressAsync(repositoryInfoId, 25, "Metadatos Git extraídos", ct);

            var findings = new List<AnalysisFinding>();

            // ── Security scan (all repos) ────────────────────────────────────────
            // Runs before C# analysis so critical security issues surface first.
            await reporter.ReportProgressAsync(repositoryInfoId, 30, "Escaneando seguridad...", ct);
            var securityFindings = await RunSecurityScanAsync(clonePath, report.Id, repositoryInfo.Trust, ct);
            findings.AddRange(securityFindings);

            // ── C# static analysis ───────────────────────────────────────────────
            // All .cs files for architecture detection (needs the full picture).
            var csFiles = Directory.GetFiles(clonePath, "*.cs", SearchOption.AllDirectories)
                .Where(f => IsAnalyzableFile(f))
                .ToArray();

            // Quality analysis additionally excludes test files and migrations.
            var qualityFiles = csFiles
                .Where(f => !IsTestFile(f) && !IsMigrationFile(f))
                .ToArray();

            // Detect primary language from file counts
            var primaryLanguage = DetectPrimaryLanguage(clonePath);
            if (primaryLanguage is not null)
            {
                repositoryInfo.UpdatePrimaryLanguage(primaryLanguage);
                await db.SaveChangesAsync(ct);
            }

            await reporter.ReportProgressAsync(repositoryInfoId, 45, $"Analizando {qualityFiles.Length} archivos C#...", ct);

            foreach (var filePath in qualityFiles)
            {
                try
                {
                    var code = await File.ReadAllTextAsync(filePath, ct);
                    var tree = CSharpSyntaxTree.ParseText(code);
                    var root = await tree.GetRootAsync(ct);
                    var relPath = filePath.Replace(clonePath, string.Empty).TrimStart('/', '\\');

                    AnalyzeSyntaxNodes(root, tree, relPath, report.Id, findings);
                    AnalyzeTrivia(root, tree, relPath, report.Id, findings);
                    AnalyzeStringLiterals(root, tree, relPath, report.Id, findings);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to analyze file {FilePath}.", filePath);
                }
            }

            // ── TypeScript / JavaScript static analysis ──────────────────────
            var tsFiles = Directory.GetFiles(clonePath, "*", SearchOption.AllDirectories)
                .Where(f =>
                {
                    var ext = Path.GetExtension(f);
                    return (ext.Equals(".ts",  StringComparison.OrdinalIgnoreCase) ||
                            ext.Equals(".tsx", StringComparison.OrdinalIgnoreCase) ||
                            ext.Equals(".js",  StringComparison.OrdinalIgnoreCase) ||
                            ext.Equals(".jsx", StringComparison.OrdinalIgnoreCase)) &&
                           IsAnalyzableFile(f) &&
                           !IsTypeScriptTestFile(f) &&
                           !IsGeneratedWebAsset(f);
                })
                .ToArray();

            if (tsFiles.Length > 0)
            {
                await reporter.ReportProgressAsync(repositoryInfoId, 60, $"Analizando {tsFiles.Length} archivos TypeScript/JavaScript...", ct);
                var tsFindings = await AnalyzeTypeScriptFilesAsync(tsFiles, clonePath, report.Id, ct);
                findings.AddRange(tsFindings);
            }

            await reporter.ReportProgressAsync(repositoryInfoId, 70, "Detectando patrones arquitectónicos...", ct);

            var architectureFindings = await DetectArchitecturalPatternsAsync(csFiles, clonePath, report.Id, ct);
            findings.AddRange(architectureFindings);

            await reporter.ReportProgressAsync(repositoryInfoId, 85, "Finalizando reporte...", ct);

            // Persist findings
            foreach (var finding in findings)
                db.AnalysisFindings.Add(finding);

            var criticalCount = findings.Count(f => f.Severity == FindingSeverity.Critical);
            var highCount     = findings.Count(f => f.Severity == FindingSeverity.High);
            var totalFiles    = csFiles.Length + tsFiles.Length;
            var summary = $"Análisis completo. Se encontraron {findings.Count} hallazgos " +
                          $"({criticalCount} críticos, {highCount} altos) en {totalFiles} archivos " +
                          $"({csFiles.Length} C#, {tsFiles.Length} TypeScript/JavaScript).";

            report.Complete(summary, findings.Count, criticalCount);
            repositoryInfo.MarkAnalyzed();
            repositoryInfo.ClearLocalPath();

            await db.SaveChangesAsync(ct);

            if (Directory.Exists(clonePath))
            {
                try { DeleteDirectory(clonePath); }
                catch (Exception cleanEx)
                {
                    logger.LogWarning(cleanEx,
                        "Analysis completed but cleanup failed for repository {RepositoryInfoId}.",
                        repositoryInfoId);
                }
            }

            try { await reporter.ReportCompleteAsync(repositoryInfoId, report.Id, ct); }
            catch (Exception reportEx)
            {
                logger.LogWarning(reportEx,
                    "Analysis completed but SignalR notification failed for repository {RepositoryInfoId}.",
                    repositoryInfoId);
            }

            logger.LogInformation(
                "Analysis completed for repository {RepositoryInfoId}. Report: {ReportId}. {Summary}",
                repositoryInfoId, report.Id, summary);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Analysis failed for repository {RepositoryInfoId}.", repositoryInfoId);

            if (report?.Status == AnalysisStatus.Completed)
                return;

            if (report is not null)
            {
                try { report.Fail(ex.Message); await db.SaveChangesAsync(ct); }
                catch (Exception saveEx) { logger.LogError(saveEx, "Failed to persist error state for report."); }
            }

            try { await reporter.ReportErrorAsync(repositoryInfoId, ex.Message, ct); }
            catch (Exception reportEx) { logger.LogError(reportEx, "Failed to report error via SignalR."); }

            if (clonePath is not null && Directory.Exists(clonePath))
            {
                try { DeleteDirectory(clonePath); }
                catch (Exception cleanEx) { logger.LogWarning(cleanEx, "Failed to clean up clone directory."); }
            }
        }
    }

    // ── Security scan ─────────────────────────────────────────────────────────

    private static async Task<List<AnalysisFinding>> RunSecurityScanAsync(
        string clonePath,
        Guid reportId,
        RepositoryTrust trust,
        CancellationToken ct)
    {
        var findings = new List<AnalysisFinding>();

        // ── 1. Build file / script execution (all repos) ─────────────────────
        // Malicious repos often hide execution in build hooks.
        var buildPatterns = new[] { "*.csproj", "*.targets", "*.props", "*.ps1", "*.sh", "*.bat", "*.cmd" };
        foreach (var pattern in buildPatterns)
        {
            var buildFiles = Directory.GetFiles(clonePath, pattern, SearchOption.AllDirectories);
            foreach (var buildFile in buildFiles)
            {
                if (!IsAnalyzableFile(buildFile)) continue;
                try
                {
                    var content = await File.ReadAllTextAsync(buildFile, ct);
                    var rel     = buildFile.Replace(clonePath, string.Empty).TrimStart('/', '\\');

                    // Code execution in build hooks
                    if (content.Contains("<Exec Command=",      StringComparison.OrdinalIgnoreCase)
                     || content.Contains("Process.Start(",      StringComparison.OrdinalIgnoreCase)
                     || content.Contains("shell.exec(",         StringComparison.OrdinalIgnoreCase)
                     || content.Contains("Invoke-Expression",   StringComparison.OrdinalIgnoreCase)
                     || content.Contains("IEX ",                StringComparison.OrdinalIgnoreCase)
                     || content.Contains("& {",                 StringComparison.OrdinalIgnoreCase))
                    {
                        findings.Add(new AnalysisFinding(
                            reportId, FindingSeverity.Critical, "Seguridad",
                            $"Ejecución de código detectada en archivo de build '{rel}' — posible backdoor o supply-chain attack.",
                            rel));
                    }

                    // Suspicious network calls in scripts
                    if (content.Contains("Invoke-WebRequest", StringComparison.OrdinalIgnoreCase)
                     || content.Contains("curl ",             StringComparison.OrdinalIgnoreCase)
                     || content.Contains("wget ",             StringComparison.OrdinalIgnoreCase)
                     || content.Contains("DownloadString",    StringComparison.OrdinalIgnoreCase)
                     || content.Contains("Net.WebClient",     StringComparison.OrdinalIgnoreCase))
                    {
                        findings.Add(new AnalysisFinding(
                            reportId, FindingSeverity.High, "Seguridad",
                            $"Descarga de recursos de red detectada en '{rel}' — verificar que sea intencional.",
                            rel));
                    }

                    // Base64 obfuscation (common in malware payloads)
                    if (content.Contains("FromBase64String", StringComparison.OrdinalIgnoreCase)
                     || content.Contains("-EncodedCommand",  StringComparison.OrdinalIgnoreCase)
                     || content.Contains("::FromBase64",     StringComparison.OrdinalIgnoreCase))
                    {
                        findings.Add(new AnalysisFinding(
                            reportId, FindingSeverity.Critical, "Seguridad",
                            $"Payload ofuscado en Base64 detectado en '{rel}' — indicador de malware.",
                            rel));
                    }
                }
                catch { /* tolerate read failures */ }
            }
        }

        // ── 2. Suspicious binary / executable files ───────────────────────────
        var executableExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".exe", ".dll", ".so", ".dylib", ".bin", ".com", ".scr", ".pif" };

        foreach (var file in Directory.GetFiles(clonePath, "*", SearchOption.AllDirectories))
        {
            if (!IsAnalyzableFile(file)) continue;

            var ext = Path.GetExtension(file);
            if (executableExtensions.Contains(ext))
            {
                var rel = file.Replace(clonePath, string.Empty).TrimStart('/', '\\');
                findings.Add(new AnalysisFinding(
                    reportId, FindingSeverity.High, "Seguridad",
                    $"Binario/ejecutable precompilado en el repositorio: '{rel}' — los binarios no deberían estar en source control.",
                    rel));
            }
        }

        // ── 3. Hardcoded secrets in ALL text files ────────────────────────────
        // (C# string literals are also checked in AnalyzeStringLiterals per-file,
        //  but here we catch secrets in .json, .yaml, .xml, .env, .config, etc.)
        var textExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".json", ".yaml", ".yml", ".xml", ".config", ".env", ".ini", ".toml", ".properties" };

        var secretPatterns = new (Regex Pattern, string Label)[]
        {
            (new Regex(@"password\s*[:=]\s*[""']?[^\s""'<]{4,}", RegexOptions.IgnoreCase | RegexOptions.Compiled), "contraseña"),
            (new Regex(@"pwd\s*[:=]\s*[""']?[^\s""'<]{4,}",      RegexOptions.IgnoreCase | RegexOptions.Compiled), "contraseña (pwd)"),
            (new Regex(@"secret\s*[:=]\s*[""']?[^\s""'<]{8,}",   RegexOptions.IgnoreCase | RegexOptions.Compiled), "secret"),
            (new Regex(@"api[_-]?key\s*[:=]\s*[""']?[^\s""'<]{8,}", RegexOptions.IgnoreCase | RegexOptions.Compiled), "API key"),
            (new Regex(@"token\s*[:=]\s*[""']?[A-Za-z0-9\-_\.]{20,}", RegexOptions.IgnoreCase | RegexOptions.Compiled), "token"),
            (new Regex(@"connectionstring\s*=.*password", RegexOptions.IgnoreCase | RegexOptions.Compiled), "cadena de conexión con contraseña"),
            (new Regex(@"mongodb\+srv?://[^:]+:[^@]+@", RegexOptions.IgnoreCase | RegexOptions.Compiled), "URI MongoDB con credenciales"),
            (new Regex(@"(postgres|postgresql|mysql|mssql|sqlserver)://[^:]+:[^@]+@", RegexOptions.IgnoreCase | RegexOptions.Compiled), "URI de BD con credenciales"),
            (new Regex(@"-----BEGIN (RSA |EC |DSA )?PRIVATE KEY-----", RegexOptions.Compiled), "clave privada PEM"),
            (new Regex(@"AKIA[0-9A-Z]{16}", RegexOptions.Compiled), "AWS Access Key ID"),
            (new Regex(@"(?:ghp|gho|ghu|ghs|ghr)_[A-Za-z0-9]{36}", RegexOptions.Compiled), "GitHub Personal Access Token"),
        };

        foreach (var file in Directory.GetFiles(clonePath, "*", SearchOption.AllDirectories))
        {
            if (!IsAnalyzableFile(file)) continue;

            var ext = Path.GetExtension(file);
            if (!textExtensions.Contains(ext)) continue;

            var filename = Path.GetFileName(file);

            // .env files are almost always secrets
            if (filename.Equals(".env", StringComparison.OrdinalIgnoreCase)
             || filename.StartsWith(".env.", StringComparison.OrdinalIgnoreCase))
            {
                var rel = file.Replace(clonePath, string.Empty).TrimStart('/', '\\');
                findings.Add(new AnalysisFinding(
                    reportId, FindingSeverity.Critical, "Seguridad",
                    $"Archivo '{rel}' en el repositorio. " +
                    $"Soluciones: (1) agregar '.env' a .gitignore y proveer un '.env.example' con valores ficticios como referencia; " +
                    $"(2) si ya fue commiteado con secrets reales, rotar inmediatamente las credenciales expuestas.",
                    rel));
                continue; // no need to scan contents too
            }

            try
            {
                var content = await File.ReadAllTextAsync(file, ct);
                var rel     = file.Replace(clonePath, string.Empty).TrimStart('/', '\\');

                foreach (var (pattern, label) in secretPatterns)
                {
                    if (pattern.IsMatch(content))
                    {
                        findings.Add(new AnalysisFinding(
                            reportId, FindingSeverity.Critical, "Seguridad",
                            $"Posible {label} hardcodeado en '{rel}'. " +
                            $"Soluciones: (1) agregar el archivo a .gitignore y usar variables de entorno o User Secrets en desarrollo; " +
                            $"(2) si el archivo debe estar en el repo (ej. appsettings de ejemplo), usar valores ficticios que nunca se repliquen en producción.",
                            rel));
                        break; // one finding per file is enough
                    }
                }
            }
            catch { /* tolerate read failures */ }
        }

        // ── 4. SQL injection patterns (in all .cs files via raw string check) ─
        // Full Roslyn-based detection runs in AnalyzeSyntaxNodes; this catches
        // string concatenation patterns even in generated or unusual code.
        var sqlInjectionPattern = new Regex(
            @"(""SELECT|""INSERT|""UPDATE|""DELETE|""EXEC|""EXECUTE|""DROP|""CREATE|""ALTER)\s",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        var allCsFiles = Directory.GetFiles(clonePath, "*.cs", SearchOption.AllDirectories)
            .Where(f => IsAnalyzableFile(f) && !IsMigrationFile(f) && !IsTestFile(f))
            .ToArray();

        foreach (var file in allCsFiles)
        {
            try
            {
                var content = await File.ReadAllTextAsync(file, ct);
                var rel     = file.Replace(clonePath, string.Empty).TrimStart('/', '\\');

                if (sqlInjectionPattern.IsMatch(content))
                {
                    // Check if it's actually a raw string concatenation (not a parameterized query)
                    var hasStringConcat = Regex.IsMatch(content,
                        @"(""SELECT|""INSERT|""UPDATE|""DELETE).*?[""]?\s*\+\s*",
                        RegexOptions.IgnoreCase | RegexOptions.Singleline);

                    if (hasStringConcat)
                    {
                        findings.Add(new AnalysisFinding(
                            reportId, FindingSeverity.Critical, "Seguridad",
                            "Posible SQL Injection: concatenación de strings en consulta SQL detectada. Usar parámetros o EF Core.",
                            rel));
                    }
                    else
                    {
                        findings.Add(new AnalysisFinding(
                            reportId, FindingSeverity.Medium, "Seguridad",
                            "SQL raw detectado — verificar que use parámetros y no concatenación de strings.",
                            rel));
                    }
                }
            }
            catch { /* tolerate read failures */ }
        }

        return findings;
    }

    // ── C# syntax analysis ────────────────────────────────────────────────────

    private static void AnalyzeSyntaxNodes(
        SyntaxNode root,
        SyntaxTree tree,
        string relPath,
        Guid reportId,
        List<AnalysisFinding> findings)
    {
        foreach (var node in root.DescendantNodes())
        {
            // ── Catch blocks ─────────────────────────────────────────────────
            if (node is CatchClauseSyntax catchClause)
            {
                var lineSpan = tree.GetLineSpan(catchClause.Span);
                var line     = lineSpan.StartLinePosition.Line + 1;

                if (catchClause.Block.Statements.Count == 0)
                {
                    findings.Add(new AnalysisFinding(
                        reportId, FindingSeverity.High, "Calidad de código",
                        "Bloque catch vacío: la excepción se silencia completamente. Agregar al menos un log.",
                        relPath, line));
                }
                else if (catchClause.Declaration is null
                      || catchClause.Declaration.Type.ToString() is "Exception" or "System.Exception")
                {
                    findings.Add(new AnalysisFinding(
                        reportId, FindingSeverity.Medium, "Calidad de código",
                        "Catch demasiado genérico: captura todas las excepciones. Capturar tipos específicos.",
                        relPath, line));
                }
            }

            // ── Class-level checks ───────────────────────────────────────────
            if (node is ClassDeclarationSyntax classDecl)
            {
                var lineSpan   = tree.GetLineSpan(classDecl.Span);
                var lineCount  = lineSpan.EndLinePosition.Line - lineSpan.StartLinePosition.Line + 1;
                var startLine  = lineSpan.StartLinePosition.Line + 1;

                // God class
                if (lineCount > 300)
                {
                    var severity = lineCount > 600 ? FindingSeverity.High : FindingSeverity.Medium;
                    findings.Add(new AnalysisFinding(
                        reportId, severity, "Calidad de código",
                        $"Clase '{classDecl.Identifier.Text}' tiene {lineCount} líneas — viola SRP. Dividir en clases más pequeñas.",
                        relPath, startLine));
                }

                // Public fields (should be properties)
                foreach (var field in classDecl.Members.OfType<FieldDeclarationSyntax>())
                {
                    if (!field.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword))) continue;
                    var fieldLine = tree.GetLineSpan(field.Span).StartLinePosition.Line + 1;
                    var names     = string.Join(", ", field.Declaration.Variables.Select(v => v.Identifier.Text));
                    findings.Add(new AnalysisFinding(
                        reportId, FindingSeverity.Medium, "Calidad de código",
                        $"Campo público '{names}' en '{classDecl.Identifier.Text}' — usar propiedades con acceso controlado.",
                        relPath, fieldLine));
                }

                // Too many constructor dependencies (SRP warning)
                var ctorMaxParams   = classDecl.Members.OfType<ConstructorDeclarationSyntax>()
                                        .Select(c => c.ParameterList.Parameters.Count)
                                        .DefaultIfEmpty(0).Max();
                var primaryParamCount = classDecl.ParameterList?.Parameters.Count ?? 0;
                var maxParams         = Math.Max(ctorMaxParams, primaryParamCount);
                if (maxParams > 7)
                {
                    findings.Add(new AnalysisFinding(
                        reportId, FindingSeverity.Medium, "Arquitectura",
                        $"Clase '{classDecl.Identifier.Text}' tiene {maxParams} dependencias en el constructor — posible violación de SRP.",
                        relPath, startLine));
                }
            }

            // ── Method-level checks ──────────────────────────────────────────
            if (node is MethodDeclarationSyntax method)
            {
                var lineSpan    = tree.GetLineSpan(method.Span);
                var methodLines = lineSpan.EndLinePosition.Line - lineSpan.StartLinePosition.Line + 1;
                var methodLine  = lineSpan.StartLinePosition.Line + 1;
                var methodName  = method.Identifier.Text;

                // Long method
                if (methodLines > 60)
                {
                    var severity = methodLines > 120 ? FindingSeverity.High : FindingSeverity.Medium;
                    findings.Add(new AnalysisFinding(
                        reportId, severity, "Calidad de código",
                        $"Método '{methodName}' tiene {methodLines} líneas — extraer en métodos más pequeños.",
                        relPath, methodLine));
                }

                // async void (fire-and-forget anti-pattern)
                var isAsync     = method.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword));
                var returnsVoid = method.ReturnType.ToString() == "void";
                if (isAsync && returnsVoid)
                {
                    findings.Add(new AnalysisFinding(
                        reportId, FindingSeverity.High, "Calidad de código",
                        $"'async void {methodName}': las excepciones no se pueden capturar. Usar 'async Task'.",
                        relPath, methodLine));
                }

                // Cyclomatic complexity
                if (method.Body is not null)
                {
                    var complexity = 1
                        + method.Body.DescendantNodes().Count(n =>
                            n is IfStatementSyntax
                            or ForStatementSyntax
                            or ForEachStatementSyntax
                            or WhileStatementSyntax
                            or DoStatementSyntax
                            or CatchClauseSyntax
                            or SwitchSectionSyntax
                            or ConditionalExpressionSyntax)
                        + method.Body.DescendantTokens().Count(t =>
                            t.IsKind(SyntaxKind.AmpersandAmpersandToken) ||
                            t.IsKind(SyntaxKind.BarBarToken));

                    if (complexity > 10)
                    {
                        var severity = complexity > 20 ? FindingSeverity.High : FindingSeverity.Medium;
                        findings.Add(new AnalysisFinding(
                            reportId, severity, "Calidad de código",
                            $"Método '{methodName}' tiene complejidad ciclomática {complexity} — difícil de testear. Refactorizar.",
                            relPath, methodLine));
                    }
                }
            }

            // ── Dangerous invocations ────────────────────────────────────────
            if (node is InvocationExpressionSyntax invocation)
            {
                var invokeText = invocation.ToString();
                var lineSpan   = tree.GetLineSpan(invocation.Span);
                var invLine    = lineSpan.StartLinePosition.Line + 1;

                // Console.WriteLine in production code
                if (invokeText.StartsWith("Console.Write", StringComparison.Ordinal))
                {
                    findings.Add(new AnalysisFinding(
                        reportId, FindingSeverity.Medium, "Calidad de código",
                        "Console.WriteLine en código de producción — usar ILogger.",
                        relPath, invLine));
                }

                // Thread.Sleep (blocks threadpool)
                if (invokeText.StartsWith("Thread.Sleep", StringComparison.Ordinal))
                {
                    findings.Add(new AnalysisFinding(
                        reportId, FindingSeverity.Medium, "Deuda técnica",
                        "Thread.Sleep bloquea el thread del pool. Usar 'await Task.Delay()' en código asíncrono.",
                        relPath, invLine));
                }

                // Process.Start in non-build code (potential code execution)
                if (invokeText.StartsWith("Process.Start", StringComparison.Ordinal))
                {
                    findings.Add(new AnalysisFinding(
                        reportId, FindingSeverity.High, "Seguridad",
                        "Process.Start en código C#: iniciar procesos externos puede ser un vector de ataque. Revisar input validation.",
                        relPath, invLine));
                }

                // Reflection-based invocation (potential code injection).
                // Check the AST node directly — NOT invokeText.Contains() — to avoid flagging
                // strings that mention these names as text (e.g., the analyzer's own patterns).
                if (invocation.Expression is MemberAccessExpressionSyntax reflectionAccess)
                {
                    var typeName   = reflectionAccess.Expression.ToString();
                    var methodName = reflectionAccess.Name.Identifier.Text;

                    var isReflectionCall =
                        (typeName is "Assembly"  && methodName is "Load" or "LoadFrom" or "LoadFile") ||
                        (typeName is "Activator" && methodName is "CreateInstance" or "CreateInstanceFrom");

                    if (isReflectionCall)
                    {
                        // Skip Activator.CreateInstance when the type comes from a same-assembly
                        // GetTypes() call — this is the standard endpoint/module auto-discovery pattern
                        // and carries no remote code execution risk.
                        var enclosingMethod = invocation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                        var methodBody      = enclosingMethod?.Body?.ToString() ?? string.Empty;
                        var isSameAssemblyPattern =
                            typeName is "Activator" &&
                            (methodBody.Contains("GetTypes()",           StringComparison.Ordinal) ||
                             methodBody.Contains("GetExecutingAssembly", StringComparison.Ordinal) ||
                             methodBody.Contains("typeof(",              StringComparison.Ordinal));

                        if (!isSameAssemblyPattern)
                        {
                            findings.Add(new AnalysisFinding(
                                reportId, FindingSeverity.High, "Seguridad",
                                $"Carga dinámica de código via reflexión ({typeName}.{methodName}) — verificar que el origen sea confiable.",
                                relPath, invLine));
                        }
                    }
                }
            }

            // ── .Result / .Wait() — sync-over-async ─────────────────────────
            if (node is MemberAccessExpressionSyntax memberAccess)
            {
                var memberName = memberAccess.Name.Identifier.Text;
                // Only flag .Result on Task-like expressions, not on any property named "Result"
                // Heuristic: the expression before .Result contains "Async", "Task", or common async method names
                if (memberName is "Result" or "Wait")
                {
                    var expr = memberAccess.Expression.ToString();
                    var looksLikeTask = expr.Contains("Async", StringComparison.OrdinalIgnoreCase)
                                     || expr.StartsWith("Task.", StringComparison.Ordinal)
                                     || expr.EndsWith("Task", StringComparison.Ordinal);
                    if (looksLikeTask)
                    {
                        var lineSpan = tree.GetLineSpan(memberAccess.Span);
                        findings.Add(new AnalysisFinding(
                            reportId, FindingSeverity.High, "Calidad de código",
                            $"Uso de .{memberName} en Task — sync-over-async puede causar deadlocks. Usar 'await'.",
                            relPath, lineSpan.StartLinePosition.Line + 1));
                    }
                }
            }
        }
    }

    // ── Comment trivia (TODO/FIXME/HACK) ─────────────────────────────────────

    private static void AnalyzeTrivia(
        SyntaxNode root,
        SyntaxTree tree,
        string relPath,
        Guid reportId,
        List<AnalysisFinding> findings)
    {
        foreach (var trivia in root.DescendantTrivia())
        {
            if (trivia.RawKind != (int)SyntaxKind.SingleLineCommentTrivia
             && trivia.RawKind != (int)SyntaxKind.MultiLineCommentTrivia)
                continue;

            var text = trivia.ToString();
            // Use word-boundary regex to avoid false positives like "Hacker" matching "HACK".
            if (!Regex.IsMatch(text, @"\b(TODO|FIXME|HACK|XXX)\b", RegexOptions.IgnoreCase))
                continue;

            var lineSpan    = tree.GetLineSpan(trivia.Span);
            var trimmedText = text.Trim();
            if (trimmedText.Length > 200) trimmedText = trimmedText[..200];

            findings.Add(new AnalysisFinding(
                reportId, FindingSeverity.Low, "Deuda técnica",
                trimmedText, relPath, lineSpan.StartLinePosition.Line + 1));
        }
    }

    // ── String literal secrets (in C# source) ────────────────────────────────

    private static void AnalyzeStringLiterals(
        SyntaxNode root,
        SyntaxTree tree,
        string relPath,
        Guid reportId,
        List<AnalysisFinding> findings)
    {
        var secretKeywords = new[]
        {
            "password=", "pwd=", "secret=", "apikey=", "api_key=", "access_token=",
            "connectionstring=", "mongodb://", "mongodb+srv://", "Server=;Password=",
            "Data Source=;Password=", "-----BEGIN PRIVATE KEY-----",
        };

        foreach (var literal in root.DescendantNodes().OfType<LiteralExpressionSyntax>())
        {
            if (literal.Kind() != SyntaxKind.StringLiteralExpression) continue;
            var value = literal.Token.ValueText;

            // Minimum length: short strings are parameter names or search patterns, not credentials.
            if (string.IsNullOrWhiteSpace(value) || value.Length < 12) continue;

            // Strings that end with '=' are search patterns (e.g. "password=", "connectionstring=").
            if (value.TrimEnd().EndsWith('=')) continue;

            // Strings containing regex meta-characters are detection patterns, not values.
            if (value.IndexOfAny(['*', '^', '$', '|', '?', '[', ']', '(', ')']) >= 0) continue;

            if (secretKeywords.Any(k => value.Contains(k, StringComparison.OrdinalIgnoreCase)))
            {
                var lineSpan = tree.GetLineSpan(literal.Span);
                findings.Add(new AnalysisFinding(
                    reportId, FindingSeverity.Critical, "Seguridad",
                    "Credencial o cadena de conexión hardcodeada en código fuente C#.",
                    relPath, lineSpan.StartLinePosition.Line + 1));
            }
        }
    }

    // ── Architectural pattern detection ───────────────────────────────────────

    internal static async Task<List<AnalysisFinding>> DetectArchitecturalPatternsAsync(
        string[] csFiles,
        string clonePath,
        Guid reportId,
        CancellationToken ct)
    {
        var repoInterfaces    = new HashSet<string>(StringComparer.Ordinal);
        var repoClasses       = new HashSet<string>(StringComparer.Ordinal);
        var dbContextFiles    = new HashSet<string>(StringComparer.Ordinal);
        var mediatorFiles     = new HashSet<string>(StringComparer.Ordinal);
        var minimalApiFiles   = new HashSet<string>(StringComparer.Ordinal);
        var controllerFiles   = new HashSet<string>(StringComparer.Ordinal);
        var featureFolderFiles = new HashSet<string>(StringComparer.Ordinal);

        var repoIfacePattern  = new Regex(@"^I\w+Repository$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        var repoClassPattern  = new Regex(@"Repository$",      RegexOptions.Compiled | RegexOptions.IgnoreCase);
        var mediatorNames     = new HashSet<string>(StringComparer.Ordinal) { "IMediator", "ISender", "IRequestHandler" };
        var minimalApiMethods = new HashSet<string>(StringComparer.Ordinal)
            { "MapGet", "MapPost", "MapPut", "MapDelete", "MapPatch", "MapGroup", "MapEndpoints" };

        foreach (var filePath in csFiles)
        {
            try
            {
                var code = await File.ReadAllTextAsync(filePath, ct);
                var root = await CSharpSyntaxTree.ParseText(code).GetRootAsync(ct);

                foreach (var node in root.DescendantNodes())
                {
                    if (node is InterfaceDeclarationSyntax iface &&
                        repoIfacePattern.IsMatch(iface.Identifier.Text))
                        repoInterfaces.Add(filePath);

                    if (node is ClassDeclarationSyntax cls &&
                        repoClassPattern.IsMatch(cls.Identifier.Text) &&
                        cls.Identifier.Text is not ("RepositoryInfo" or "RepositoryTrust"))
                        repoClasses.Add(filePath);

                    if (node is ConstructorDeclarationSyntax ctor)
                    {
                        foreach (var param in ctor.ParameterList.Parameters)
                        {
                            var typeName = param.Type?.ToString() ?? string.Empty;
                            if (typeName.EndsWith("Context", StringComparison.Ordinal) ||
                                typeName.StartsWith("IApplicationDbContext", StringComparison.Ordinal))
                            { dbContextFiles.Add(filePath); break; }
                        }
                    }

                    if (node is ClassDeclarationSyntax primaryCtorCls &&
                        primaryCtorCls.ParameterList is { Parameters.Count: > 0 } primaryParams)
                    {
                        foreach (var param in primaryParams.Parameters)
                        {
                            var typeName = param.Type?.ToString() ?? string.Empty;
                            if (typeName.EndsWith("Context", StringComparison.Ordinal) ||
                                typeName.StartsWith("IApplicationDbContext", StringComparison.Ordinal))
                            { dbContextFiles.Add(filePath); break; }
                        }
                    }

                    if (node is IdentifierNameSyntax id && mediatorNames.Contains(id.Identifier.Text))
                        mediatorFiles.Add(filePath);

                    if (node is InvocationExpressionSyntax inv)
                    {
                        var methodName = (inv.Expression as MemberAccessExpressionSyntax)
                                          ?.Name.Identifier.Text ?? string.Empty;
                        if (minimalApiMethods.Contains(methodName))
                            minimalApiFiles.Add(filePath);
                    }

                    if (node is ClassDeclarationSyntax ctrlCls)
                    {
                        var hasAttr = ctrlCls.AttributeLists
                            .SelectMany(a => a.Attributes)
                            .Any(a => a.Name.ToString() is "ApiController" or "Controller");
                        var inherits = ctrlCls.BaseList?.Types
                            .Any(t => t.Type.ToString() is "ControllerBase" or "Controller") ?? false;
                        if (hasAttr || inherits) controllerFiles.Add(filePath);
                    }
                }

                if (filePath.Contains("/Features/",  StringComparison.OrdinalIgnoreCase) ||
                    filePath.Contains(@"\Features\", StringComparison.OrdinalIgnoreCase))
                    featureFolderFiles.Add(filePath);
            }
            catch { /* tolerate parse failures */ }
        }

        var findings = new List<AnalysisFinding>();

        // Repository pattern mixed with direct DbContext — conflicting conventions
        if (repoInterfaces.Count > 0 && dbContextFiles.Count > 0)
            findings.Add(new AnalysisFinding(
                reportId, FindingSeverity.Medium, "Arquitectura",
                $"Convención de acceso a datos inconsistente: se detectaron {repoInterfaces.Count} interfaz/interfaces IXxxRepository " +
                $"Y {dbContextFiles.Count} uso/s de inyección directa de DbContext. " +
                "Estandarizar en uno solo: o patrón Repository o DbContext directo (al estilo Jason Taylor)."));

        // API style
        if (controllerFiles.Count > 0 && minimalApiFiles.Count > 0)
            findings.Add(new AnalysisFinding(
                reportId, FindingSeverity.Medium, "Arquitectura",
                $"Estilo de API mixto: {minimalApiFiles.Count} archivo/s con Minimal API y {controllerFiles.Count} Controller/s MVC. " +
                "Estandarizar en un único enfoque para consistencia y mantenibilidad."));
        else if (controllerFiles.Count > 0 && minimalApiFiles.Count == 0)
            findings.Add(new AnalysisFinding(
                reportId, FindingSeverity.Low, "Oportunidad de mejora",
                $"Se detectaron {controllerFiles.Count} Controller/s MVC. " +
                "Minimal API (disponible desde .NET 6) es el enfoque moderno recomendado: menos boilerplate, mejor rendimiento y más fácil de testear. " +
                "No es un problema — es una oportunidad de modernización si el proyecto lo permite."));

        return findings;
    }

    // ── TypeScript / JavaScript static analysis (regex-based) ────────────────

    private static readonly (Regex Pattern, FindingSeverity Severity, string Category, string Message)[] TsQualityPatterns =
    [
        // Security
        (new Regex(@"\beval\s*\(",                           RegexOptions.Compiled), FindingSeverity.Critical, "Seguridad",   "Uso de eval() — vector de inyección de código. Reemplazar con alternativas seguras."),
        (new Regex(@"\bnew\s+Function\s*\(",                 RegexOptions.Compiled), FindingSeverity.Critical, "Seguridad",   "new Function() ejecuta código arbitrario — equivalente a eval(). Evitar."),
        (new Regex(@"dangerouslySetInnerHTML",                RegexOptions.Compiled), FindingSeverity.High,     "Seguridad",   "dangerouslySetInnerHTML puede causar XSS si el contenido no está sanitizado. Revisar el origen del HTML."),
        (new Regex(@"innerHTML\s*=",                         RegexOptions.Compiled), FindingSeverity.High,     "Seguridad",   "Asignación directa a innerHTML — riesgo de XSS si el valor viene del usuario. Usar textContent o sanitizar."),
        (new Regex(@"document\.write\s*\(",                  RegexOptions.Compiled), FindingSeverity.High,     "Seguridad",   "document.write() es un vector de XSS y bloquea el parser. No usar."),
        (new Regex(@"localStorage\.(setItem|getItem)\s*\(.*(?i:token|password|secret|key)", RegexOptions.Compiled | RegexOptions.IgnoreCase), FindingSeverity.High, "Seguridad", "Almacenar credenciales/tokens en localStorage es inseguro (accessible via XSS). Usar httpOnly cookies."),

        // TypeScript-specific quality
        (new Regex(@":\s*any\b",                             RegexOptions.Compiled), FindingSeverity.Medium,   "Calidad de código", "Uso de tipo 'any' — pierde los beneficios de TypeScript. Tipar correctamente."),
        (new Regex(@"@ts-ignore",                            RegexOptions.Compiled), FindingSeverity.Medium,   "Calidad de código", "@ts-ignore suprime errores de TypeScript sin resolverlos. Usar @ts-expect-error y documentar por qué."),
        (new Regex(@"@ts-nocheck",                           RegexOptions.Compiled), FindingSeverity.High,     "Calidad de código", "@ts-nocheck deshabilita TypeScript en todo el archivo. Tipar correctamente en vez de silenciar."),
        (new Regex(@"as\s+any\b",                            RegexOptions.Compiled), FindingSeverity.Medium,   "Calidad de código", "Cast 'as any' — type escape hatch. Usar el tipo correcto o un type guard."),

        // Console logs in production code
        (new Regex(@"\bconsole\.(log|warn|error|debug)\s*\(", RegexOptions.Compiled), FindingSeverity.Low,    "Calidad de código", "console.log en código — usar un logger estructurado o eliminar antes de producción."),

        // TODO / FIXME / HACK comments
        (new Regex(@"//\s*(TODO|FIXME|HACK|XXX)\b",         RegexOptions.Compiled | RegexOptions.IgnoreCase), FindingSeverity.Low, "Deuda técnica", "Comentario TODO/FIXME/HACK — registrar como issue y resolver."),

        // Hardcoded credentials in TS/JS
        // Requires the value to be a quoted string literal (single, double, or backtick).
        // Without quotes the match is a variable assignment, function call, or type annotation — NOT a credential.
        (new Regex(@"(?i)(api[_-]?key|secret|password|token)\s*[:=]\s*[`""'][^`""'<]{8,}[`""']", RegexOptions.Compiled), FindingSeverity.Critical, "Seguridad", "Posible credencial hardcodeada en TypeScript/JavaScript. Usar variables de entorno."),

        // Promise anti-patterns
        (new Regex(@"\.then\s*\([^)]*\)\s*\.catch\s*\(\s*\(\s*\)\s*=>\s*\{\s*\}\s*\)", RegexOptions.Compiled), FindingSeverity.Medium, "Calidad de código", "Catch vacío en Promise — los errores se silencian. Manejar o propagar."),
        (new Regex(@"catch\s*\([^)]*\)\s*\{\s*\}",          RegexOptions.Compiled), FindingSeverity.Medium,   "Calidad de código", "Bloque catch vacío — la excepción/error se silencia completamente."),

        // Deprecated patterns
        (new Regex(@"\bvar\s+\w",                            RegexOptions.Compiled), FindingSeverity.Low,      "Calidad de código", "Uso de 'var' — preferir 'const' o 'let' para scope explícito."),
    ];

    private static async Task<List<AnalysisFinding>> AnalyzeTypeScriptFilesAsync(
        string[] files,
        string clonePath,
        Guid reportId,
        CancellationToken ct)
    {
        var findings = new List<AnalysisFinding>();

        foreach (var filePath in files)
        {
            try
            {
                var content = await File.ReadAllTextAsync(filePath, ct);
                var relPath = filePath.Replace(clonePath, string.Empty).TrimStart('/', '\\');
                var lines   = content.Split('\n');

                // Per-file line count check — TSX/JSX components naturally include markup + logic,
                // so the threshold is intentionally higher than for C# files.
                if (lines.Length > 400)
                {
                    var severity = lines.Length > 700 ? FindingSeverity.High : FindingSeverity.Medium;
                    findings.Add(new AnalysisFinding(
                        reportId, severity, "Calidad de código",
                        $"Archivo '{Path.GetFileName(filePath)}' tiene {lines.Length} líneas — dividir en módulos más pequeños.",
                        relPath));
                }

                // Pattern-based checks — report first match per pattern per file
                foreach (var (pattern, severity, category, message) in TsQualityPatterns)
                {
                    var match = pattern.Match(content);
                    if (!match.Success) continue;

                    // Find the line number of the first match
                    var lineNumber = content[..match.Index].Count(c => c == '\n') + 1;
                    findings.Add(new AnalysisFinding(reportId, severity, category, message, relPath, lineNumber));
                }

                // Function length check: count lines between function/arrow-function declarations
                // Heuristic: look for patterns like "function foo(" or "const foo = (" and count until next top-level declaration
                CheckTsFunctionLengths(content, lines, relPath, reportId, findings);
            }
            catch (Exception)
            {
                // tolerate read/parse failures — same policy as C# analyzer
            }
        }

        return findings;
    }

    private static readonly Regex TsFunctionStartPattern = new(
        @"^(?:export\s+)?(?:default\s+)?(?:async\s+)?function\s+(\w+)\s*[\(<]|^(?:export\s+)?const\s+(\w+)\s*=\s*(?:async\s*)?\(",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static void CheckTsFunctionLengths(
        string content,
        string[] lines,
        string relPath,
        Guid reportId,
        List<AnalysisFinding> findings)
    {
        var matches = TsFunctionStartPattern.Matches(content);

        for (var i = 0; i < matches.Count; i++)
        {
            var startLine = content[..matches[i].Index].Count(c => c == '\n');
            var endLine   = i + 1 < matches.Count
                ? content[..matches[i + 1].Index].Count(c => c == '\n')
                : lines.Length - 1;

            var length = endLine - startLine;
            // TSX/JSX components include JSX markup, hooks, handlers and render logic together —
            // 150 lines is a realistic threshold before it becomes a refactoring concern.
            if (length < 150) continue;

            var name   = matches[i].Groups[1].Success ? matches[i].Groups[1].Value
                                                      : matches[i].Groups[2].Value;
            var sev    = length > 300 ? FindingSeverity.High : FindingSeverity.Medium;

            findings.Add(new AnalysisFinding(
                reportId, sev, "Calidad de código",
                $"Función '{name}' tiene ~{length} líneas — extraer en funciones más pequeñas.",
                relPath, startLine + 1));
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Central gate for all file scans. Returns false for build output, sandboxes,
    /// version-control internals, and package caches that should never be analyzed.
    /// </summary>
    private static bool IsAnalyzableFile(string filePath)
    {
        var segments = new[]
        {
            @"\obj\",   "/obj/",
            @"\bin\",   "/bin/",
            @"\.git\",  "/.git/",
            @"\node_modules\", "/node_modules/",
            @"\packages\",     "/packages/",
            @"\vendor\",       "/vendor/",
            // Exclude the analyzer's own Guardian folder so it doesn't flag its own detection patterns
            @"\Guardian\",     "/Guardian/",
        };

        foreach (var seg in segments)
            if (filePath.Contains(seg, StringComparison.OrdinalIgnoreCase))
                return false;

        // Exclude Roslyn-generated designer files
        if (filePath.EndsWith(".g.cs",         StringComparison.OrdinalIgnoreCase) ||
            filePath.EndsWith(".designer.cs",   StringComparison.OrdinalIgnoreCase) ||
            filePath.EndsWith(".generated.cs",  StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    /// <summary>
    /// Returns true for EF Core migration files. SQL raw is expected here — not a finding.
    /// </summary>
    private static bool IsMigrationFile(string filePath) =>
        filePath.Contains(@"\Migrations\", StringComparison.OrdinalIgnoreCase) ||
        filePath.Contains("/Migrations/",  StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns true for test project files — identified by common test folder/file naming conventions.
    /// These are excluded from quality analysis to avoid false positives on intentionally long test classes.
    /// </summary>
    private static bool IsTestFile(string filePath) =>
        filePath.Contains(@"\Tests\",     StringComparison.OrdinalIgnoreCase) ||
        filePath.Contains("/Tests/",      StringComparison.OrdinalIgnoreCase) ||
        filePath.Contains(@"\Test\",      StringComparison.OrdinalIgnoreCase) ||
        filePath.Contains("/Test/",       StringComparison.OrdinalIgnoreCase) ||
        filePath.Contains(@"\Specs\",     StringComparison.OrdinalIgnoreCase) ||
        filePath.Contains("/Specs/",      StringComparison.OrdinalIgnoreCase) ||
        Path.GetFileName(filePath).EndsWith("Tests.cs",    StringComparison.OrdinalIgnoreCase) ||
        Path.GetFileName(filePath).EndsWith("Test.cs",     StringComparison.OrdinalIgnoreCase) ||
        Path.GetFileName(filePath).EndsWith("Specs.cs",    StringComparison.OrdinalIgnoreCase) ||
        Path.GetFileName(filePath).EndsWith("Fixture.cs",  StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns true for TypeScript/JavaScript test files — excluded from quality analysis.
    /// Covers Jest, Vitest, Playwright, Cypress, and Storybook conventions.
    /// </summary>
    private static bool IsTypeScriptTestFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        return filePath.Contains(@"\__tests__\",  StringComparison.OrdinalIgnoreCase) ||
               filePath.Contains("/__tests__/",   StringComparison.OrdinalIgnoreCase) ||
               filePath.Contains(@"\__mocks__\",  StringComparison.OrdinalIgnoreCase) ||
               filePath.Contains("/__mocks__/",   StringComparison.OrdinalIgnoreCase) ||
               filePath.Contains(@"\mocks\",      StringComparison.OrdinalIgnoreCase) ||
               filePath.Contains("/mocks/",       StringComparison.OrdinalIgnoreCase) ||
               filePath.Contains(@"\e2e\",         StringComparison.OrdinalIgnoreCase) ||
               filePath.Contains("/e2e/",          StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".test.ts",       StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".test.tsx",      StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".test.js",       StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".spec.ts",       StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".spec.tsx",      StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".spec.js",       StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".stories.ts",    StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".stories.tsx",   StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".cy.ts",         StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".cy.js",         StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns true for generated/static web assets that should never be analyzed for quality.
    /// Covers MSW service workers, Next.js generated files, bundler output in public/.
    /// </summary>
    private static bool IsGeneratedWebAsset(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        return fileName.Equals("mockServiceWorker.js",  StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("mockServiceWorker.ts",  StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".min.js",              StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".bundle.js",           StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".chunk.js",            StringComparison.OrdinalIgnoreCase) ||
               filePath.Contains(@"\public\",            StringComparison.OrdinalIgnoreCase) ||
               filePath.Contains("/public/",             StringComparison.OrdinalIgnoreCase) ||
               filePath.Contains(@"\.next\",             StringComparison.OrdinalIgnoreCase) ||
               filePath.Contains("/.next/",              StringComparison.OrdinalIgnoreCase) ||
               filePath.Contains(@"\dist\",              StringComparison.OrdinalIgnoreCase) ||
               filePath.Contains("/dist/",               StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Detects the primary programming language by counting source files per extension.
    /// Returns null if the project is clearly C#-only (no need to state the obvious).
    /// </summary>
    private static string? DetectPrimaryLanguage(string clonePath)
    {
        var extensionMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { ".cs",   "C#" },
            { ".ts",   "TypeScript" },
            { ".tsx",  "TypeScript" },
            { ".js",   "JavaScript" },
            { ".jsx",  "JavaScript" },
            { ".py",   "Python" },
            { ".go",   "Go" },
            { ".rs",   "Rust" },
            { ".java", "Java" },
            { ".kt",   "Kotlin" },
            { ".rb",   "Ruby" },
            { ".php",  "PHP" },
            { ".swift","Swift" },
            { ".cpp",  "C++" },
            { ".c",    "C" },
            { ".fs",   "F#" },
        };

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var file in Directory.GetFiles(clonePath, "*", SearchOption.AllDirectories))
            {
                if (file.Contains(@"\obj\") || file.Contains("/obj/") ||
                    file.Contains(@"\bin\") || file.Contains("/bin/") ||
                    file.Contains(@"\node_modules\") || file.Contains("/node_modules/"))
                    continue;

                var ext = Path.GetExtension(file);
                if (!extensionMap.TryGetValue(ext, out var lang)) continue;

                counts.TryGetValue(lang, out var current);
                counts[lang] = current + 1;
            }
        }
        catch { /* tolerate filesystem errors */ }

        if (counts.Count == 0) return null;

        return counts.OrderByDescending(kv => kv.Value).First().Key;
    }

    private static void DeleteDirectory(string targetDir)
    {
        if (string.IsNullOrWhiteSpace(targetDir) || !Directory.Exists(targetDir)) return;
        foreach (var file in Directory.GetFiles(targetDir, "*.*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(targetDir, true);
    }
}
