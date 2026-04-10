import { http, HttpResponse } from 'msw'

const BASE_URL = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000'

const MOCK_REPOSITORIES = [
  {
    id: 'repo-001',
    knowledgeItemId: 'ki-004',
    gitUrl: 'https://github.com/dotnet/aspire',
    primaryLanguage: 'C#',
    trustLevel: 'Internal',
    stars: 3200,
    openIssues: 42,
    lastCommitDate: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000).toISOString(),
    processingStatus: 'Completed',
    scope: 'Shared',
    createdAt: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000).toISOString(),
  },
  {
    id: 'repo-002',
    knowledgeItemId: 'ki-006',
    gitUrl: 'https://github.com/dotnet/extensions',
    primaryLanguage: 'C#',
    trustLevel: 'Internal',
    stars: 1800,
    openIssues: 15,
    lastCommitDate: new Date(Date.now() - 5 * 24 * 60 * 60 * 1000).toISOString(),
    processingStatus: 'Pending',
    scope: 'Personal',
    createdAt: new Date(Date.now() - 1 * 24 * 60 * 60 * 1000).toISOString(),
  },
]

const MOCK_REPORTS: Record<string, object> = {
  'repo-001': {
    repositoryInfo: {
      gitUrl: 'https://github.com/dotnet/aspire',
      primaryLanguage: 'C#',
      trustLevel: 'Internal',
      stars: 3200,
      openIssues: 42,
      lastCommitDate: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000).toISOString(),
    },
    reports: [
      {
        id: 'rpt-001',
        analysisType: 'Full',
        overallHealthScore: 87.5,
        securityScore: 92.0,
        qualityScore: 84.0,
        maintainabilityScore: 86.5,
        findingsCount: { critical: 0, high: 1, medium: 3, low: 6 },
        executedAt: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000).toISOString(),
        analysisDurationSeconds: 38,
      },
    ],
  },
}

const MOCK_FINDINGS = [
  {
    id: 'fnd-001',
    severity: 'High',
    category: 'Dependency',
    title: 'Outdated package: Microsoft.Extensions.Http.Polly',
    description: 'Package is 2 major versions behind. Security patches may be missing.',
    filePath: 'src/Directory.Packages.props',
    recommendation: 'Upgrade to version 9.x or later',
    isResolved: false,
  },
  {
    id: 'fnd-002',
    severity: 'Medium',
    category: 'Quality',
    title: 'High cyclomatic complexity in ResourceOrchestrator',
    description: 'Method ConfigureResources has cyclomatic complexity of 18 (threshold: 10).',
    filePath: 'src/Aspire.Hosting/ResourceOrchestrator.cs',
    recommendation: 'Break down the method into smaller, more focused functions.',
    isResolved: false,
  },
  {
    id: 'fnd-003',
    severity: 'Low',
    category: 'Quality',
    title: 'Missing XML documentation on public APIs',
    description: '34 public methods are missing XML documentation comments.',
    filePath: 'src/Aspire.Hosting/',
    recommendation: 'Add <summary> comments to all public APIs.',
    isResolved: false,
  },
]

export const repositoryHandlers = [
  http.post(`${BASE_URL}/api/v1/repositories`, async ({ request }) => {
    const body = await request.json() as { gitUrl?: string; trustLevel?: string }
    return HttpResponse.json(
      {
        knowledgeItemId: `ki-${Date.now()}`,
        repositoryInfoId: `repo-${Date.now()}`,
        processingStatus: 'Pending',
        message: 'Repository queued for cloning and analysis',
        gitUrl: body.gitUrl,
        trustLevel: body.trustLevel ?? 'External',
      },
      { status: 202 },
    )
  }),

  http.get(`${BASE_URL}/api/v1/repositories`, () => {
    return HttpResponse.json({ items: MOCK_REPOSITORIES, totalCount: MOCK_REPOSITORIES.length })
  }),

  http.get(`${BASE_URL}/api/v1/repositories/:knowledgeItemId/analysis`, ({ params }) => {
    const report = MOCK_REPORTS[params.knowledgeItemId as string]
    if (!report) return new HttpResponse(null, { status: 404 })
    return HttpResponse.json(report)
  }),

  http.get(`${BASE_URL}/api/v1/repositories/:knowledgeItemId/analysis/:reportId/findings`, () => {
    return HttpResponse.json({ items: MOCK_FINDINGS, totalCount: MOCK_FINDINGS.length })
  }),

  http.post(`${BASE_URL}/api/v1/repositories/:knowledgeItemId/reanalyze`, () => {
    return HttpResponse.json(
      { reportId: `rpt-${Date.now()}`, message: 'Re-analysis queued' },
      { status: 202 },
    )
  }),
]
