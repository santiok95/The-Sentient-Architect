import { http, HttpResponse } from 'msw'
import { MOCK_REPO_IDS } from './repository.handlers'

import { getApiBaseUrl } from '@/lib/config'

const BASE_URL = getApiBaseUrl()

// Mock repo for RepoBound conversations — UUID matches repository.handlers
const MOCK_REPO = {
  id: MOCK_REPO_IDS.aspire,
  url: 'https://github.com/dotnet/aspire',
  branch: 'main',
}

interface MockConversation {
  id: string
  title: string
  agentType: 'Knowledge' | 'Consultant'
  mode: 'Auto' | 'RepoBound' | 'StackBound' | 'Generic'
  status: string
  lastMessageAt: string
  messageCount: number
  activeRepositoryId?: string
  activeRepositoryUrl?: string
  activeRepositoryBranch?: string
}

let MOCK_CONVERSATIONS: MockConversation[] = [
  {
    id: 'conv-001',
    title: 'Clean Architecture + Aspire integration',
    agentType: 'Consultant',
    mode: 'RepoBound',
    status: 'Active',
    lastMessageAt: new Date(Date.now() - 10 * 60 * 1000).toISOString(),
    messageCount: 6,
    activeRepositoryId: MOCK_REPO.id,
    activeRepositoryUrl: MOCK_REPO.url,
    activeRepositoryBranch: MOCK_REPO.branch,
  },
  {
    id: 'conv-002',
    title: 'CQRS implementation approach',
    agentType: 'Consultant',
    mode: 'Generic',
    status: 'Active',
    lastMessageAt: new Date(Date.now() - 3 * 60 * 60 * 1000).toISOString(),
    messageCount: 4,
  },
  {
    id: 'conv-003',
    title: 'pgvector schema design',
    agentType: 'Knowledge',
    mode: 'Auto',
    status: 'Archived',
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

  http.post(`${BASE_URL}/api/v1/conversations`, async ({ request }) => {
    const body = await request.json() as {
      title?: string
      agentType?: 'Knowledge' | 'Consultant'
      activeRepositoryId?: string
    }

    const agentType = body.agentType ?? 'Knowledge'
    const isRepoBound = agentType === 'Consultant' && !!body.activeRepositoryId

    const newConv: MockConversation = {
      id: `conv-${Date.now()}`,
      title: body.title ?? 'Nueva consulta',
      agentType,
      mode: isRepoBound ? 'RepoBound' : 'Auto',
      status: 'Active',
      lastMessageAt: new Date().toISOString(),
      messageCount: 0,
      ...(isRepoBound && {
        activeRepositoryId: body.activeRepositoryId,
        activeRepositoryUrl: MOCK_REPO.url,
        activeRepositoryBranch: MOCK_REPO.branch,
      }),
    }

    MOCK_CONVERSATIONS = [newConv, ...MOCK_CONVERSATIONS]
    return HttpResponse.json({ conversationId: newConv.id, title: newConv.title }, { status: 201 })
  }),

  http.get(`${BASE_URL}/api/v1/conversations/:id`, ({ params }) => {
    const conv = MOCK_CONVERSATIONS.find((c) => c.id === params.id)
    if (!conv) return new HttpResponse(null, { status: 404 })
    return HttpResponse.json({
      id: conv.id,
      title: conv.title,
      agentType: conv.agentType,
      mode: conv.mode,
      status: conv.status,
      messageCount: conv.messageCount,
      createdAt: conv.lastMessageAt,
      updatedAt: conv.lastMessageAt,
      recentMessages: MOCK_MESSAGES_BY_CONV[params.id as string] ?? [],
      activeRepositoryId: conv.activeRepositoryId ?? null,
      activeRepositoryUrl: conv.activeRepositoryUrl ?? null,
      activeRepositoryBranch: conv.activeRepositoryBranch ?? null,
    })
  }),

  http.post(`${BASE_URL}/api/v1/conversations/:id/messages`, async () => {
    return HttpResponse.json(
      { messageId: `msg-${Date.now()}`, status: 'Processing', message: 'Response streaming via ConversationHub' },
      { status: 202 },
    )
  }),

  http.post(`${BASE_URL}/api/v1/conversations/:id/chat`, async () => {
    return HttpResponse.json(
      { messageId: `msg-${Date.now()}`, status: 'Processing', message: 'Response streaming via ConversationHub' },
      { status: 202 },
    )
  }),

  http.patch(`${BASE_URL}/api/v1/conversations/:id/archive`, ({ params }) => {
    const conv = MOCK_CONVERSATIONS.find((c) => c.id === params.id)
    if (conv) conv.status = 'Archived'
    return HttpResponse.json({ archived: true })
  }),

  http.delete(`${BASE_URL}/api/v1/conversations/:id`, ({ params }) => {
    MOCK_CONVERSATIONS = MOCK_CONVERSATIONS.filter((c) => c.id !== params.id)
    return new HttpResponse(null, { status: 204 })
  }),

  http.patch(`${BASE_URL}/api/v1/conversations/:id`, async ({ params, request }) => {
    const body = await request.json() as Record<string, unknown>
    const conv = MOCK_CONVERSATIONS.find((c) => c.id === params.id)
    return HttpResponse.json({ ...conv, ...body })
  }),
]
