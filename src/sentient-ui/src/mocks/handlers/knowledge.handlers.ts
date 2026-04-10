import { http, HttpResponse } from 'msw'

const BASE_URL = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000'

export const knowledgeHandlers = [
  http.get(`${BASE_URL}/api/knowledge`, () => {
    return HttpResponse.json({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 20,
    })
  }),

  http.post(`${BASE_URL}/api/knowledge`, () => {
    return HttpResponse.json(
      {
        id: 'mock-knowledge-id',
        title: 'Mock Knowledge Item',
        type: 'Article',
        status: 'Pending',
        scope: 'Personal',
        createdAt: new Date().toISOString(),
      },
      { status: 201 },
    )
  }),

  http.post(`${BASE_URL}/api/knowledge/search`, async ({ request }) => {
    return HttpResponse.json({
      results: [],
      totalCount: 0,
    })
  }),

  http.delete(`${BASE_URL}/api/knowledge/:id`, () => {
    return new HttpResponse(null, { status: 204 })
  }),
]
