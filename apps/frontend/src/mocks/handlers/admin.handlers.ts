import { http, HttpResponse } from 'msw'
import type { PublishRequest } from '@/lib/api.types'

import { getApiBaseUrl } from '@/lib/config'

const BASE = getApiBaseUrl() + '/api/v1'

export let MOCK_REQUESTS: PublishRequest[] = [
  {
    id: 'pr-001',
    knowledgeItem: {
      id: 'ki-001',
      title: 'Clean Architecture en .NET 9 — Guía Práctica',
      type: 'Article',
      summary: 'Walkthrough completo de Clean Architecture con Application/Domain/Infrastructure en .NET 9, EF Core 9 y Result pattern.',
    },
    requestedBy: { id: 'user-001', displayName: 'Santiago López', role: 'User' },
    requestReason: 'Artículo de alta calidad con ejemplos reales. Útil para todo el equipo.',
    status: 'Pending',
    createdAt: '2026-04-08T10:30:00Z',
  },
  {
    id: 'pr-002',
    knowledgeItem: {
      id: 'ki-002',
      title: 'Semantic Kernel — Plugin Architecture Deep Dive',
      type: 'Documentation',
      summary: 'Análisis detallado del sistema de plugins en SK 1.x con ejemplos de KernelFunction y FunctionChoiceBehavior.Auto().',
    },
    requestedBy: { id: 'user-002', displayName: 'Valentina Torres', role: 'User' },
    requestReason: 'Documentación de referencia para el equipo de IA. Validada contra la documentación oficial.',
    status: 'Pending',
    createdAt: '2026-04-08T14:15:00Z',
  },
  {
    id: 'pr-003',
    knowledgeItem: {
      id: 'ki-003',
      title: 'pgvector HNSW Index Tuning',
      type: 'Note',
      summary: 'Notas de optimización para índices HNSW en pgvector: m, ef_construction y ef_search.',
    },
    requestedBy: { id: 'user-003', displayName: 'Marcos Herrera', role: 'User' },
    requestReason: 'Notas técnicas con métricas reales de performance. Relevante para el equipo de DB.',
    status: 'Pending',
    createdAt: '2026-04-09T09:00:00Z',
  },
  {
    id: 'pr-004',
    knowledgeItem: {
      id: 'ki-004',
      title: 'Result Pattern con discriminated unions en TypeScript',
      type: 'Article',
      summary: 'Implementación del Result<T, E> pattern en TS usando discriminated unions sin excepciones.',
    },
    requestedBy: { id: 'user-001', displayName: 'Santiago López', role: 'User' },
    requestReason: 'Complementa muy bien las notas de Clean Architecture del backend.',
    status: 'Approved',
    createdAt: '2026-04-07T11:00:00Z',
    reviewedAt: '2026-04-07T16:30:00Z',
  },
  {
    id: 'pr-005',
    knowledgeItem: {
      id: 'ki-005',
      title: 'Docker + WSL2 Performance Tips',
      type: 'Note',
      summary: 'Trucos para mejorar I/O de Docker Desktop en Windows con WSL2 backend.',
    },
    requestedBy: { id: 'user-004', displayName: 'Julia Ramírez', role: 'User' },
    requestReason: 'Tips útiles para el setup de desarrollo. Muy buscado en el equipo.',
    status: 'Rejected',
    createdAt: '2026-04-06T08:00:00Z',
    reviewedAt: '2026-04-06T12:00:00Z',
  },
]

export const adminHandlers = [
  // GET /api/v1/admin/publish-requests
  http.get(`${BASE}/admin/publish-requests`, ({ request }) => {
    const url = new URL(request.url)
    const status = url.searchParams.get('status')
    const page = parseInt(url.searchParams.get('page') ?? '1', 10)
    const pageSize = parseInt(url.searchParams.get('pageSize') ?? '20', 10)

    let filtered = MOCK_REQUESTS
    if (status) filtered = filtered.filter((r) => r.status === status)

    const start = (page - 1) * pageSize
    const items = filtered.slice(start, start + pageSize)

    return HttpResponse.json({ items, totalCount: filtered.length, page, pageSize })
  }),

  // PATCH /api/v1/admin/publish-requests/:id
  http.patch(`${BASE}/admin/publish-requests/:id`, async ({ params, request }) => {
    const body = await request.json() as { action: string; rejectionReason?: string }
    const id = params.id as string

    const idx = MOCK_REQUESTS.findIndex((r) => r.id === id)
    if (idx === -1) return HttpResponse.json({ title: 'Not found' }, { status: 404 })

    const newStatus = body.action === 'Approve' ? 'Approved' : 'Rejected'
    MOCK_REQUESTS[idx] = {
      ...MOCK_REQUESTS[idx],
      status: newStatus,
      reviewedAt: new Date().toISOString(),
    }

    // Simulate 400ms network latency for realism
    await new Promise((r) => setTimeout(r, 400))

    return HttpResponse.json({
      id,
      status: newStatus,
      reviewedAt: MOCK_REQUESTS[idx].reviewedAt,
    })
  }),

  // GET /api/v1/admin/users
  http.get(`${BASE}/admin/users`, () => {
    return HttpResponse.json({
      items: [
        { id: 'user-001', email: 'santiago@sa.dev', displayName: 'Santiago López', role: 'Admin', createdAt: '2025-10-01T00:00:00Z', todayTokenUsage: 45000 },
        { id: 'user-002', email: 'valentina@sa.dev', displayName: 'Valentina Torres', role: 'User', createdAt: '2025-11-15T00:00:00Z', todayTokenUsage: 12500 },
        { id: 'user-003', email: 'marcos@sa.dev', displayName: 'Marcos Herrera', role: 'User', createdAt: '2025-12-01T00:00:00Z', todayTokenUsage: 8200 },
        { id: 'user-004', email: 'julia@sa.dev', displayName: 'Julia Ramírez', role: 'User', createdAt: '2026-01-10T00:00:00Z', todayTokenUsage: 3400 },
      ],
    })
  }),
]
