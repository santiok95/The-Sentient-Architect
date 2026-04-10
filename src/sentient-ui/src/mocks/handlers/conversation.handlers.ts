import { http, HttpResponse } from 'msw'

const BASE_URL = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000'

const MOCK_CONVERSATIONS = [
  {
    id: 'conv-001',
    title: 'Clean Architecture + Aspire integration',
    objective: 'Integrar Aspire con Clean Architecture sin romper separación de capas.',
    status: 'Active',
    lastMessageAt: new Date(Date.now() - 10 * 60 * 1000).toISOString(),
    messageCount: 6,
  },
  {
    id: 'conv-002',
    title: 'CQRS implementation approach',
    objective: '¿Usar MediatR o implementar CQRS manualmente?',
    status: 'Active',
    lastMessageAt: new Date(Date.now() - 3 * 60 * 60 * 1000).toISOString(),
    messageCount: 4,
  },
  {
    id: 'conv-003',
    title: 'pgvector schema design',
    objective: 'Optimizar el schema de embeddings para búsquedas multi-tenant.',
    status: 'Completed',
    lastMessageAt: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000).toISOString(),
    messageCount: 12,
  },
]

const MOCK_MESSAGES_BY_CONV: Record<string, Array<{ id: string; role: string; content: string; createdAt: string }>> = {
  'conv-001': [
    {
      id: 'msg-001',
      role: 'Assistant',
      content: 'Hola. Estoy analizando el contexto de `dotnet/aspire`. ¿En qué aspecto de la arquitectura querés trabajar hoy?',
      createdAt: new Date(Date.now() - 15 * 60 * 1000).toISOString(),
    },
    {
      id: 'msg-002',
      role: 'User',
      content: 'Quiero entender cómo integrar Aspire con Clean Architecture sin romper la separación de capas.',
      createdAt: new Date(Date.now() - 14 * 60 * 1000).toISOString(),
    },
    {
      id: 'msg-003',
      role: 'Assistant',
      content: 'La clave está en que Aspire vive en la capa de `Presentation/Hosting`, no en Application ni Domain. El `AppHost` es el punto de entrada de orquestación.',
      createdAt: new Date(Date.now() - 13 * 60 * 1000).toISOString(),
    },
  ],
}

export const conversationHandlers = [
  http.get(`${BASE_URL}/api/v1/conversations`, ({ request }) => {
    const url = new URL(request.url)
    const status = url.searchParams.get('status')
    let items = [...MOCK_CONVERSATIONS]
    if (status) items = items.filter((c) => c.status === status)
    return HttpResponse.json({ items, totalCount: items.length, page: 1, pageSize: 20 })
  }),

  http.post(`${BASE_URL}/api/v1/conversations`, async () => {
    const newConv = {
      id: `conv-${Date.now()}`,
      title: 'New conversation',
      objective: null,
      status: 'Active',
      lastMessageAt: new Date().toISOString(),
      messageCount: 0,
    }
    return HttpResponse.json(newConv, { status: 201 })
  }),

  http.get(`${BASE_URL}/api/v1/conversations/:id`, ({ params }) => {
    const conv = MOCK_CONVERSATIONS.find((c) => c.id === params.id)
    if (!conv) return new HttpResponse(null, { status: 404 })
    return HttpResponse.json({
      ...conv,
      recentMessages: MOCK_MESSAGES_BY_CONV[params.id as string] ?? [],
      latestSummary: null,
    })
  }),

  http.post(`${BASE_URL}/api/v1/conversations/:id/messages`, async ({ request }) => {
    const body = await request.json() as { content?: string }
    return HttpResponse.json(
      { messageId: `msg-${Date.now()}`, status: 'Processing', message: 'Response streaming via ConversationHub' },
      { status: 202 },
    )
  }),

  http.patch(`${BASE_URL}/api/v1/conversations/:id`, async ({ params, request }) => {
    const body = await request.json() as Record<string, unknown>
    const conv = MOCK_CONVERSATIONS.find((c) => c.id === params.id)
    return HttpResponse.json({ ...conv, ...body })
  }),
]
