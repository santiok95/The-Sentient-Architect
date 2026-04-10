/**
 * lib/api-client.ts
 * Typed HTTP client with JWT auth header injection, auto-refresh on 401, and Result Pattern.
 *
 * ⚠️  SINGLETON RULE: Only one instance. Never instantiate inside a component.
 * Import { apiClient } from '@/lib/api-client' and call its methods.
 */

// ─── Result Pattern (mirrors .NET backend) ───────────────────────────────────

export type ApiSuccess<T> = { ok: true; data: T; status: number }
export type ApiError = { ok: false; error: string; status: number; details?: unknown }
export type ApiResult<T> = ApiSuccess<T> | ApiError

// ─── Token Storage (client-side only, never SSR) ─────────────────────────────

const TOKEN_KEY = 'sa_token'
const REFRESH_KEY = 'sa_refresh'

function getToken(): string | null {
  if (typeof window === 'undefined') return null
  return localStorage.getItem(TOKEN_KEY)
}

function getRefreshToken(): string | null {
  if (typeof window === 'undefined') return null
  return localStorage.getItem(REFRESH_KEY)
}

export function saveTokens(token: string, refreshToken: string): void {
  localStorage.setItem(TOKEN_KEY, token)
  localStorage.setItem(REFRESH_KEY, refreshToken)
}

export function clearTokens(): void {
  localStorage.removeItem(TOKEN_KEY)
  localStorage.removeItem(REFRESH_KEY)
}

// ─── Core Fetch Wrapper ───────────────────────────────────────────────────────

const BASE_URL = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000'

type RequestOptions = Omit<RequestInit, 'body'> & {
  body?: unknown
  skipAuth?: boolean
}

let isRefreshing = false
let refreshQueue: Array<(token: string | null) => void> = []

async function tryRefresh(): Promise<string | null> {
  const refreshToken = getRefreshToken()
  if (!refreshToken) return null

  try {
    const res = await fetch(`${BASE_URL}/api/auth/refresh`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken }),
    })

    if (!res.ok) {
      clearTokens()
      return null
    }

    const data = (await res.json()) as { token: string; refreshToken: string }
    saveTokens(data.token, data.refreshToken)
    return data.token
  } catch {
    clearTokens()
    return null
  }
}

async function request<T>(
  path: string,
  options: RequestOptions = {},
): Promise<ApiResult<T>> {
  const { body, skipAuth = false, headers: extraHeaders, ...restOptions } = options

  const buildHeaders = (token: string | null): HeadersInit => ({
    'Content-Type': 'application/json',
    Accept: 'application/json',
    ...(token && !skipAuth ? { Authorization: `Bearer ${token}` } : {}),
    ...(extraHeaders as Record<string, string>),
  })

  const makeRequest = async (token: string | null): Promise<Response> =>
    fetch(`${BASE_URL}${path}`, {
      ...restOptions,
      headers: buildHeaders(token),
      body: body !== undefined ? JSON.stringify(body) : undefined,
    })

  let token = skipAuth ? null : getToken()
  let response = await makeRequest(token)

  // 401: attempt token refresh once
  if (response.status === 401 && !skipAuth) {
    if (!isRefreshing) {
      isRefreshing = true
      const newToken = await tryRefresh()
      isRefreshing = false
      refreshQueue.forEach((resolve) => resolve(newToken))
      refreshQueue = []
      token = newToken
    } else {
      token = await new Promise<string | null>((resolve) => {
        refreshQueue.push(resolve)
      })
    }

    if (!token) {
      // Redirect to login — post message for the auth store to handle
      if (typeof window !== 'undefined') {
        window.dispatchEvent(new CustomEvent('sa:unauthorized'))
      }
      return { ok: false, error: 'Session expired', status: 401 }
    }

    response = await makeRequest(token)
  }

  if (!response.ok) {
    let errorMessage = `HTTP ${response.status}`
    let details: unknown
    try {
      const errBody = await response.json()
      errorMessage = errBody?.message ?? errBody?.title ?? errorMessage
      details = errBody
    } catch {
      // body wasn't JSON — keep default message
    }
    return { ok: false, error: errorMessage, status: response.status, details }
  }

  if (response.status === 204) {
    return { ok: true, data: undefined as T, status: 204 }
  }

  const data = (await response.json()) as T
  return { ok: true, data, status: response.status }
}

// ─── Public API ───────────────────────────────────────────────────────────────

export const apiClient = {
  get: <T>(path: string, options?: RequestOptions) =>
    request<T>(path, { method: 'GET', ...options }),

  post: <T>(path: string, body: unknown, options?: RequestOptions) =>
    request<T>(path, { method: 'POST', body, ...options }),

  put: <T>(path: string, body: unknown, options?: RequestOptions) =>
    request<T>(path, { method: 'PUT', body, ...options }),

  patch: <T>(path: string, body: unknown, options?: RequestOptions) =>
    request<T>(path, { method: 'PATCH', body, ...options }),

  delete: <T = void>(path: string, options?: RequestOptions) =>
    request<T>(path, { method: 'DELETE', ...options }),
}
