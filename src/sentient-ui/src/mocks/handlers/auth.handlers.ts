import { http, HttpResponse } from 'msw'

const BASE_URL = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000'

export const authHandlers = [
  http.post(`${BASE_URL}/api/auth/login`, () => {
    return HttpResponse.json({
      token: 'mock-jwt-token',
      refreshToken: 'mock-refresh-token',
      expiresIn: 3600,
      user: {
        id: 'mock-user-id',
        email: 'dev@sentient.io',
        displayName: 'Dev User',
        role: 'User',
        tenantId: 'mock-tenant-id',
      },
    })
  }),

  http.post(`${BASE_URL}/api/auth/register`, () => {
    return HttpResponse.json(
      { message: 'Registration successful' },
      { status: 201 },
    )
  }),

  http.post(`${BASE_URL}/api/auth/refresh`, () => {
    return HttpResponse.json({
      token: 'mock-jwt-token-refreshed',
      refreshToken: 'mock-refresh-token-refreshed',
      expiresIn: 3600,
    })
  }),

  http.post(`${BASE_URL}/api/auth/logout`, () => {
    return new HttpResponse(null, { status: 204 })
  }),
]
