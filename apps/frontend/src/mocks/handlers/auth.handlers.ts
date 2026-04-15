import { http, HttpResponse } from 'msw'

import { getApiBaseUrl } from '@/lib/config'

const BASE_URL = getApiBaseUrl()

export const authHandlers = [
  http.post(`${BASE_URL}/api/v1/auth/login`, async ({ request }) => {
    const body = await request.json() as { email?: string }
    const isAdmin = body?.email?.includes('admin') ?? false
    return HttpResponse.json({
      token: isAdmin ? 'mock-jwt-admin-token' : 'mock-jwt-token',
      refreshToken: 'mock-refresh-token',
      expiresIn: 3600,
      user: {
        id: isAdmin ? 'mock-admin-id' : 'mock-user-id',
        email: body?.email ?? 'dev@sentient.io',
        displayName: isAdmin ? 'Admin User' : 'Dev User',
        role: isAdmin ? 'Admin' : 'User',
        tenantId: 'mock-tenant-id',
      },
    })
  }),

  http.post(`${BASE_URL}/api/v1/auth/register`, () => {
    return HttpResponse.json(
      { message: 'Registration successful' },
      { status: 201 },
    )
  }),

  http.post(`${BASE_URL}/api/v1/auth/refresh`, () => {
    return HttpResponse.json({
      token: 'mock-jwt-token-refreshed',
      refreshToken: 'mock-refresh-token-refreshed',
      expiresIn: 3600,
    })
  }),

  http.post(`${BASE_URL}/api/v1/auth/logout`, () => {
    return new HttpResponse(null, { status: 204 })
  }),
]
