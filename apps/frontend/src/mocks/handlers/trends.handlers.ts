import { http, HttpResponse } from 'msw'
import type { Trend } from '@/lib/api.types'

import { getApiBaseUrl } from '@/lib/config'

const BASE = getApiBaseUrl() + '/api/v1'

const MOCK_TRENDS: Trend[] = [
  {
    id: 'trend-001',
    name: '.NET Aspire',
    category: 'Framework',
    tractionLevel: 'Growing',
    relevanceScore: 92.5,
    summary: 'Opinionated cloud-native stack para apps .NET distribuidas. Orquesta servicios, observabilidad y health checks out of the box.',
    sources: ['https://devblogs.microsoft.com/dotnet', 'https://github.com/dotnet/aspire'],
    lastUpdatedAt: '2026-04-08T14:30:00Z',
    starCount: 3800,
    gitHubUrl: 'https://github.com/dotnet/aspire',
  },
  {
    id: 'trend-002',
    name: 'Semantic Kernel',
    category: 'Innovation',
    tractionLevel: 'Growing',
    relevanceScore: 88.0,
    summary: 'SDK de Microsoft para integrar LLMs en apps .NET, Python y Java. Arquitectura de plugins con planners y memory abstractions.',
    sources: ['https://github.com/microsoft/semantic-kernel'],
    lastUpdatedAt: '2026-04-09T08:00:00Z',
    starCount: 22400,
    gitHubUrl: 'https://github.com/microsoft/semantic-kernel',
  },
  {
    id: 'trend-003',
    name: 'Vertical Slice Architecture',
    category: 'Architecture',
    tractionLevel: 'Mainstream',
    relevanceScore: 79.0,
    summary: 'Organización de codebases por feature slice en lugar de capa técnica. Reduce acoplamiento y mejora cohesión.',
    sources: ['https://jimmybogard.com/vertical-slice-architecture'],
    lastUpdatedAt: '2026-03-20T10:00:00Z',
    starCount: null,
    gitHubUrl: null,
  },
  {
    id: 'trend-004',
    name: 'Clean Architecture',
    category: 'BestPractice',
    tractionLevel: 'Mainstream',
    relevanceScore: 83.5,
    summary: 'Separación de concerns en capas con dependencias apuntando hacia el dominio. Facilita testability y mantenibilidad.',
    sources: ['https://github.com/jasontaylordev/CleanArchitecture'],
    lastUpdatedAt: '2026-04-07T16:45:00Z',
    starCount: 16200,
    gitHubUrl: 'https://github.com/jasontaylordev/CleanArchitecture',
  },
  {
    id: 'trend-005',
    name: 'OpenTelemetry',
    category: 'DevOps',
    tractionLevel: 'Growing',
    relevanceScore: 85.0,
    summary: 'Estándar abierto para instrumentación de observabilidad. Unifica traces, métricas y logs en un solo SDK.',
    sources: ['https://opentelemetry.io', 'https://github.com/open-telemetry/opentelemetry-dotnet'],
    lastUpdatedAt: '2026-04-05T09:00:00Z',
    starCount: 3100,
    gitHubUrl: 'https://github.com/open-telemetry/opentelemetry-dotnet',
  },
  {
    id: 'trend-006',
    name: 'Domain-Driven Design',
    category: 'BestPractice',
    tractionLevel: 'Mainstream',
    relevanceScore: 74.0,
    summary: 'Modelado del software en base al dominio del negocio. Aggregates, bounded contexts y ubiquitous language.',
    sources: ['https://github.com/ddd-crew'],
    lastUpdatedAt: '2026-01-15T12:00:00Z',
    starCount: null,
    gitHubUrl: null,
  },
  {
    id: 'trend-007',
    name: 'Testcontainers',
    category: 'Testing',
    tractionLevel: 'Growing',
    relevanceScore: 71.0,
    summary: 'Librería para integration tests con contenedores Docker reales. Elimina mocks de infraestructura en tests.',
    sources: ['https://testcontainers.com', 'https://github.com/testcontainers/testcontainers-dotnet'],
    lastUpdatedAt: '2026-03-30T11:00:00Z',
    starCount: 3600,
    gitHubUrl: 'https://github.com/testcontainers/testcontainers-dotnet',
  },
  {
    id: 'trend-008',
    name: 'Event-Driven Architecture',
    category: 'Architecture',
    tractionLevel: 'Growing',
    relevanceScore: 76.5,
    summary: 'Comunicación asíncrona entre servicios vía eventos. Desacoplamiento fuerte con alta escalabilidad.',
    sources: ['https://github.com/topics/event-driven'],
    lastUpdatedAt: '2026-02-20T08:30:00Z',
    starCount: null,
    gitHubUrl: null,
  },
  {
    id: 'trend-009',
    name: 'AI-Augmented Development',
    category: 'Innovation',
    tractionLevel: 'Emerging',
    relevanceScore: 80.0,
    summary: 'Integración de LLMs en el ciclo de desarrollo: code review, generación de tests y documentación automatizada.',
    sources: ['https://github.com/topics/ai-coding'],
    lastUpdatedAt: '2026-03-01T13:00:00Z',
    starCount: null,
    gitHubUrl: null,
  },
  {
    id: 'trend-010',
    name: 'Platform Engineering',
    category: 'DevOps',
    tractionLevel: 'Growing',
    relevanceScore: 68.0,
    summary: 'Construcción de Internal Developer Platforms (IDP) para mejorar la experiencia del desarrollador a escala.',
    sources: ['https://platformengineering.org'],
    lastUpdatedAt: '2026-01-10T09:00:00Z',
    starCount: null,
    gitHubUrl: null,
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
        { tractionLevel: 'Emerging',   sentimentScore: 68, snapshotDate: '2025-11-15', notes: null },
        { tractionLevel: 'Emerging',   sentimentScore: 72, snapshotDate: '2025-12-15', notes: null },
        { tractionLevel: 'Growing',    sentimentScore: 79, snapshotDate: '2026-01-15', notes: null },
        { tractionLevel: 'Growing',    sentimentScore: 83, snapshotDate: '2026-02-15', notes: null },
        { tractionLevel: 'Growing',    sentimentScore: 88, snapshotDate: '2026-03-15', notes: null },
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
