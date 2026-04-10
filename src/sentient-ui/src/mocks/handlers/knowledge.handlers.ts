import { http, HttpResponse } from 'msw'

const BASE_URL = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000'

const MOCK_ITEMS = [
  {
    id: 'ki-001',
    title: 'CQRS + Event Sourcing in .NET 9',
    type: 'Article',
    summary: 'Deep dive into CQRS pattern combined with Event Sourcing using MediatR and MongoDB.',
    sourceUrl: 'https://blog.example.com/cqrs-dotnet9',
    tags: ['CQRS', '.NET', 'Architecture'],
    processingStatus: 'Completed',
    scope: 'Shared',
    createdAt: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(),
  },
  {
    id: 'ki-002',
    title: 'Vertical Slice Architecture Guide',
    type: 'Documentation',
    summary: 'How to organize code by features instead of technical layers.',
    sourceUrl: null,
    tags: ['Architecture', 'Clean Code'],
    processingStatus: 'Completed',
    scope: 'Shared',
    createdAt: new Date(Date.now() - 5 * 60 * 60 * 1000).toISOString(),
  },
  {
    id: 'ki-003',
    title: 'pgvector Performance Benchmarks',
    type: 'Note',
    summary: 'My personal benchmarks comparing HNSW vs IVFFlat indexes on 1M vectors.',
    sourceUrl: null,
    tags: ['PostgreSQL', 'AI', 'Performance'],
    processingStatus: 'Processing',
    scope: 'Personal',
    createdAt: new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString(),
  },
  {
    id: 'ki-004',
    title: 'github.com/dotnet/aspire',
    type: 'Repository',
    summary: '.NET Aspire — opinionated stack for building resilient cloud-native apps.',
    sourceUrl: 'https://github.com/dotnet/aspire',
    tags: ['Aspire', '.NET', 'Cloud'],
    processingStatus: 'Completed',
    scope: 'Shared',
    createdAt: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000).toISOString(),
  },
  {
    id: 'ki-005',
    title: 'Rate Limiting Patterns for APIs',
    type: 'Article',
    summary: 'Token bucket, sliding window, and fixed window algorithms compared.',
    sourceUrl: 'https://blog.example.com/rate-limiting',
    tags: ['API', 'Patterns', 'Performance'],
    processingStatus: 'Pending',
    scope: 'Personal',
    createdAt: new Date(Date.now() - 3 * 24 * 60 * 60 * 1000).toISOString(),
  },
]

export const knowledgeHandlers = [
  http.get(`${BASE_URL}/api/v1/knowledge`, ({ request }) => {
    const url = new URL(request.url)
    const page = parseInt(url.searchParams.get('page') ?? '1')
    const pageSize = parseInt(url.searchParams.get('pageSize') ?? '20')
    const type = url.searchParams.get('type')
    const search = url.searchParams.get('search')

    let items = [...MOCK_ITEMS]
    if (type) items = items.filter((i) => i.type === type)
    if (search) items = items.filter((i) => i.title.toLowerCase().includes(search.toLowerCase()))

    const start = (page - 1) * pageSize
    return HttpResponse.json({
      items: items.slice(start, start + pageSize),
      totalCount: items.length,
      page,
      pageSize,
    })
  }),

  http.post(`${BASE_URL}/api/v1/knowledge`, async ({ request }) => {
    const body = await request.json() as Record<string, unknown>
    return HttpResponse.json(
      {
        id: `ki-${Date.now()}`,
        processingStatus: 'Pending',
        message: 'Content queued for processing',
        ...body,
        createdAt: new Date().toISOString(),
      },
      { status: 202 },
    )
  }),

  http.post(`${BASE_URL}/api/v1/knowledge/search`, async ({ request }) => {
    const body = await request.json() as { query?: string }
    const q = body?.query ?? ''
    const matched = MOCK_ITEMS.filter(
      (i) =>
        i.title.toLowerCase().includes(q.toLowerCase()) ||
        (i.summary ?? '').toLowerCase().includes(q.toLowerCase()),
    )
    return HttpResponse.json({
      results: matched.map((i) => ({
        knowledgeItemId: i.id,
        title: i.title,
        matchedChunk: i.summary ?? i.title,
        similarityScore: 0.85 + Math.random() * 0.1,
        type: i.type,
        tags: i.tags,
        scope: i.scope,
      })),
      queryEmbeddingTimeMs: 42,
      searchTimeMs: 85,
    })
  }),

  http.delete(`${BASE_URL}/api/v1/knowledge/:id`, () => {
    return new HttpResponse(null, { status: 204 })
  }),

  http.post(`${BASE_URL}/api/v1/knowledge/:id/publish`, () => {
    return HttpResponse.json(
      { publishRequestId: `pr-${Date.now()}`, status: 'Pending' },
      { status: 202 },
    )
  }),

  http.get(`${BASE_URL}/api/v1/tags`, () => {
    return HttpResponse.json({
      items: [
        { id: 't1', name: '.NET', category: 'Technology', usageCount: 12 },
        { id: 't2', name: 'Architecture', category: 'Concept', usageCount: 9 },
        { id: 't3', name: 'CQRS', category: 'Pattern', usageCount: 5 },
        { id: 't4', name: 'PostgreSQL', category: 'Technology', usageCount: 7 },
        { id: 't5', name: 'AI', category: 'Technology', usageCount: 4 },
      ],
    })
  }),
]
