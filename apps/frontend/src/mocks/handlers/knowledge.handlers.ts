import { http, HttpResponse } from 'msw'
import { MOCK_REQUESTS } from './admin.handlers'
import type { PublishRequest } from '@/lib/api.types'

import { getApiBaseUrl } from '@/lib/config'

const BASE_URL = getApiBaseUrl()

let MOCK_ITEMS: {
  id: string
  title: string
  type: 'Article' | 'Note' | 'Documentation' | 'Repository'
  summary: string
  sourceUrl: string | null
  tags: string[]
  processingStatus: 'Pending' | 'Processing' | 'Completed' | 'Failed'
  scope: 'Personal' | 'Shared'
  createdAt: string
}[] = [
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
    const isAdmin = request.headers.get('authorization')?.toLowerCase().includes('admin') ?? false
    const newId = `ki-${Date.now()}`
    const title = (body.title as string) ?? 'Nuevo artículo'
    const type = (body.type as string) ?? 'Article'

    // Add to local mock items so GET reflects the new item
    MOCK_ITEMS.push({
      id: newId,
      title,
      type: type as typeof MOCK_ITEMS[0]['type'],
      summary: ((body.content as string) ?? '').slice(0, 120) || title,
      sourceUrl: (body.sourceUrl as string | null) ?? null,
      tags: (body.tags as string[]) ?? [],
      processingStatus: 'Completed',
      scope: 'Personal',
      createdAt: new Date().toISOString(),
    })

    // Non-admin users get an automatic publish request
    if (!isAdmin) {
      const pr: PublishRequest = {
        id: `pr-${Date.now()}`,
        knowledgeItem: { id: newId, title, type, summary: '' },
        requestedBy: { id: 'user-current', displayName: 'Vos', role: 'User' },
        requestReason: undefined,
        status: 'Pending',
        createdAt: new Date().toISOString(),
      }
      MOCK_REQUESTS.push(pr)
    }

    return HttpResponse.json(
      {
        id: newId,
        title,
        type,
        status: 'Completed',
        chunksCreated: 1,
        createdAt: new Date().toISOString(),
      },
      { status: 201 },
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

  http.post(`${BASE_URL}/api/v1/knowledge/:id/publish`, ({ request }) => {
    const isAdmin = request.headers.get('authorization')?.includes('admin') ?? false
    const status = isAdmin ? 'Approved' : 'Pending'
    return HttpResponse.json(
      { publishRequestId: `pr-${Date.now()}`, status },
      { status: 202 },
    )
  }),

  http.get(`${BASE_URL}/api/v1/knowledge/my-publish-requests`, ({ request }) => {
    const url = new URL(request.url)
    const page = parseInt(url.searchParams.get('page') ?? '1', 10)
    const pageSize = parseInt(url.searchParams.get('pageSize') ?? '20', 10)

    // Return the subset of MOCK_REQUESTS that belong to the "current user" (user-current or user-001)
    const mine = MOCK_REQUESTS.filter((r) =>
      r.requestedBy.id === 'user-current' || r.requestedBy.id === 'user-001',
    )
    const start = (page - 1) * pageSize
    const items = mine.slice(start, start + pageSize).map((r) => ({
      id: r.id,
      knowledgeItemId: r.knowledgeItem.id,
      knowledgeItemTitle: r.knowledgeItem.title,
      knowledgeItemType: r.knowledgeItem.type,
      requestReason: r.requestReason,
      status: r.status,
      createdAt: r.createdAt,
      reviewedAt: r.reviewedAt,
      rejectionReason: undefined,
    }))

    return HttpResponse.json({ items, totalCount: mine.length, page, pageSize })
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
