# Security Rules — Repository Analysis

## Cardinal Rule
NEVER execute code from analyzed repositories. All analysis is static → read files, parse AST, check dependencies. No compilation, no test execution, no script running.

## Trust Levels
- **External repos**: Full security scan. Check for malicious scripts in build files, vulnerable dependencies, suspicious patterns, license issues.
- **Internal repos**: Quality-focused. Skip malware scanning, but still check third-party dependency vulnerabilities.

## Cloning
- Clone to `temp-repos/` directory (gitignored)
- Delete cloned repo after analysis completes
- Never persist repo source code in the database → only metadata and analysis results
- Set reasonable size limits on cloneable repos

## Dependency Scanning
- Check NuGet packages against known vulnerability databases
- Flag packages with no recent updates (>2 years)
- Detect license incompatibilities
- Both trust levels check dependencies (you don't control third-party code)

## What to Analyze (Static Only)
- Code complexity (cyclomatic, cognitive)
- Pattern detection (anti-patterns, code smells)
- Dependency graph and version health
- README and documentation quality
- Test coverage indicators (presence of test projects/files)
- Git metadata (commit frequency, contributor count, last activity)
