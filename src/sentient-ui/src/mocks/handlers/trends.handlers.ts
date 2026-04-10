import { http, HttpResponse } from 'msw'
import type { Trend } from '@/lib/api.types'

const BASE = 'http://localhost:5000/api/v1'

const MOCK_TRENDS: Trend[] = [
  {
    id: 'trend-001',
    name: '.NET Aspire',
    category: 'Framework',
    tractionLevel: 'Growing',
    relevanceScore: 92.5,
    summary: 'Opinionated cloud-native stack for building distributed .NET applications. Orchestrates services, observability and health checks out of the box.',
    sources: ['https://devblogs.microsoft.com/dotnet', 'https://github.com/dotnet/aspire'],
    firstDetectedAt: '2025-11-01T00:00:00Z',
    lastUpdatedAt: '2026-04-08T14:30:00Z',
  },
  {
    id: 'trend-002',
    name: 'Semantic Kernel',
    category: 'AI/ML',
    tractionLevel: 'Growing',
    relevanceScore: 88.0,
    summary: 'Microsoft SDK for integrating LLMs into .NET, Python and Java apps. Plugin architecture with planners and memory abstractions.',
    sources: ['https://github.com/microsoft/semantic-kernel', 'https://learn.microsoft.com/semantic-kernel'],
    firstDetectedAt: '2025-09-15T00:00:00Z',
    lastUpdatedAt: '2026-04-09T08:00:00Z',
  },
  {
    id: 'trend-003',
    name: 'Vertical Slice Architecture',
    category: 'Architecture',
    tractionLevel: 'Mainstream',
    relevanceScore: 79.0,
    summary: 'Organizing codebases by feature slice instead of technical layer. Reduces coupling and improves cohesion in medium-to-large systems.',
    sources: ['https://jimmybogard.com/vertical-slice-architecture', 'https://github.com/jbogard/ContosoUniversityDotNetCore'],
    firstDetectedAt: '2025-03-01T00:00:00Z',
    lastUpdatedAt: '2026-03-20T10:00:00Z',
  },
  {
    id: 'trend-004',
    name: 'pgvector',
    category: 'Database',
    tractionLevel: 'Growing',
    relevanceScore: 83.5,
    summary: 'PostgreSQL extension for vector similarity search. Enables RAG and semantic search without a separate vector database.',
    sources: ['https://github.com/pgvector/pgvector', 'https://supabase.com/docs/guides/database/extensions/pgvector'],
    firstDetectedAt: '2025-06-10T00:00:00Z',
    lastUpdatedAt: '2026-04-07T16:45:00Z',
  },
  {
    id: 'trend-005',
    name: 'React Server Components',
    category: 'Frontend',
    tractionLevel: 'Mainstream',
    relevanceScore: 85.0,
    summary: 'React primitive for server-rendered components with no client bundle cost. Foundation of Next.js App Router.',
    sources: ['https://react.dev/reference/rsc/server-components', 'https://nextjs.org/docs/app/building-your-application/rendering/server-components'],
    firstDetectedAt: '2024-12-01T00:00:00Z',
    lastUpdatedAt: '2026-04-05T09:00:00Z',
  },
  {
    id: 'trend-006',
    name: 'Minimal APIs (.NET)',
    category: 'Backend',
    tractionLevel: 'Mainstream',
    relevanceScore: 74.0,
    summary: 'Low-ceremony HTTP API style introduced in .NET 6. Reduces boilerplate vs MVC controllers for microservices and small APIs.',
    sources: ['https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis'],
    firstDetectedAt: '2023-11-01T00:00:00Z',
    lastUpdatedAt: '2026-01-15T12:00:00Z',
  },
  {
    id: 'trend-007',
    name: 'Bun',
    category: 'Runtime',
    tractionLevel: 'Emerging',
    relevanceScore: 61.0,
    summary: 'All-in-one JavaScript runtime with bundler, test runner and package manager. 3-10x faster than Node.js in benchmarks.',
    sources: ['https://bun.sh', 'https://github.com/oven-sh/bun'],
    firstDetectedAt: '2025-10-01T00:00:00Z',
    lastUpdatedAt: '2026-03-30T11:00:00Z',
  },
  {
    id: 'trend-008',
    name: 'WebAssembly WASI',
    category: 'Runtime',
    tractionLevel: 'Emerging',
    relevanceScore: 55.5,
    summary: 'WebAssembly System Interface enables portable, sandboxed execution outside of browsers. Interest growing in serverless and edge environments.',
    sources: ['https://wasi.dev', 'https://bytecodealliance.org'],
    firstDetectedAt: '2025-08-15T00:00:00Z',
    lastUpdatedAt: '2026-02-20T08:30:00Z',
  },
  {
    id: 'trend-009',
    name: 'tRPC',
    category: 'API',
    tractionLevel: 'Mainstream',
    relevanceScore: 71.0,
    summary: 'End-to-end type-safe APIs without code generation. Pairs extremely well with Next.js + TypeScript monorepos.',
    sources: ['https://trpc.io', 'https://github.com/trpc/trpc'],
    firstDetectedAt: '2024-03-01T00:00:00Z',
    lastUpdatedAt: '2026-03-01T13:00:00Z',
  },
  {
    id: 'trend-010',
    name: 'Dapper (revival)',
    category: 'ORM/Data',
    tractionLevel: 'Declining',
    relevanceScore: 42.0,
    summary: 'Lightweight .NET micro-ORM. Declining as EF Core 8+ raw queries and compiled models close the performance gap with simpler DX.',
    sources: ['https://github.com/DapperLib/Dapper'],
    firstDetectedAt: '2023-01-01T00:00:00Z',
    lastUpdatedAt: '2026-01-10T09:00:00Z',
  },
]

export const trendsHandlers = [
  // GET /api/v1/trends
  http.get(`${BASE}/trends`, ({ request }) => {
    const url = new URL(request.url)
    const category = url.searchParams.get('category')
    const traction = url.searchParams.get('traction')
    const minRelevance = parseFloat(url.searchParams.get('minRelevance') ?? '0')
    const page = parseInt(url.searchParams.get('page') ?? '1', 10)
    const pageSize = parseInt(url.searchParams.get('pageSize') ?? '20', 10)

    let filtered = MOCK_TRENDS
    if (category) filtered = filtered.filter((t) => t.category === category)
    if (traction) filtered = filtered.filter((t) => t.tractionLevel === traction)
    if (minRelevance > 0) filtered = filtered.filter((t) => t.relevanceScore >= minRelevance)

    const start = (page - 1) * pageSize
    const items = filtered.slice(start, start + pageSize)

    return HttpResponse.json({
      items,
      totalCount: filtered.length,
      page,
      pageSize,
    })
  }),

  // GET /api/v1/trends/:id/snapshots
  http.get(`${BASE}/trends/:id/snapshots`, ({ params }) => {
    return HttpResponse.json({
      trend: { id: params.id, name: 'Trending Tech' },
      snapshots: [
        { tractionLevel: 'Emerging', mentionCount: 12, sentimentScore: 0.68, snapshotDate: '2025-11-15' },
        { tractionLevel: 'Emerging', mentionCount: 23, sentimentScore: 0.72, snapshotDate: '2025-12-15' },
        { tractionLevel: 'Growing', mentionCount: 45, sentimentScore: 0.79, snapshotDate: '2026-01-15' },
        { tractionLevel: 'Growing', mentionCount: 78, sentimentScore: 0.83, snapshotDate: '2026-02-15' },
        { tractionLevel: 'Growing', mentionCount: 112, sentimentScore: 0.88, snapshotDate: '2026-03-15' },
      ],
    })
  }),

  // POST /api/v1/admin/trends/sync
  http.post(`${BASE}/admin/trends/sync`, () => {
    return HttpResponse.json(
      { message: 'Trend scan queued', estimatedDurationMinutes: 5 },
      { status: 202 },
    )
  }),
]
