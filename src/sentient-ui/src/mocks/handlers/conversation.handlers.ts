import { http, HttpResponse } from 'msw'

const BASE_URL = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000'

export const conversationHandlers = [
  http.get(`${BASE_URL}/api/conversations`, () => {
    return HttpResponse.json({
      items: [],
      totalCount: 0,
    })
  }),

  http.post(`${BASE_URL}/api/conversations`, () => {
    return HttpResponse.json(
      {
        id: 'mock-conv-id',
        title: 'New Conversation',
        status: 'Active',
        mode: 'Auto',
        createdAt: new Date().toISOString(),
      },
      { status: 201 },
    )
  }),

  http.post(`${BASE_URL}/api/conversations/:id/messages`, () => {
    return HttpResponse.json({
      id: 'mock-msg-id',
      content: 'Mock assistant response',
      role: 'Assistant',
      createdAt: new Date().toISOString(),
    })
  }),

  http.get(`${BASE_URL}/api/conversations/:id/messages`, () => {
    return HttpResponse.json({
      items: [],
      totalCount: 0,
    })
  }),
]
