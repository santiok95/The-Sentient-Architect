/**
 * lib/auth.ts
 * JWT helpers and auth flow utilities.
 * Works client-side only. SSR-safe guard on localStorage access.
 */
import { apiClient, saveTokens, clearTokens } from './api-client'
import type { LoginInput, RegisterInput } from './schemas'

// ─── Types ────────────────────────────────────────────────────────────────────

export interface AuthUser {
  id: string
  email: string
  displayName: string
  role: 'Admin' | 'User'
  tenantId: string
}

export interface AuthSession {
  user: AuthUser
  token: string
  refreshToken: string
  expiresIn: number
}

// ─── Token Accessors (imported by signalr.ts too) ────────────────────────────

const TOKEN_KEY = 'sa_token'

export function getToken(): string | null {
  if (typeof window === 'undefined') return null
  return localStorage.getItem(TOKEN_KEY)
}

export function isAuthenticated(): boolean {
  return getToken() !== null
}

// ─── Auth Operations ──────────────────────────────────────────────────────────

export async function login(
  input: LoginInput,
): Promise<{ ok: true; session: AuthSession } | { ok: false; error: string }> {
  const result = await apiClient.post<AuthSession>('/api/auth/login', input, {
    skipAuth: true,
  })

  if (!result.ok) return { ok: false, error: result.error }

  saveTokens(result.data.token, result.data.refreshToken)
  return { ok: true, session: result.data }
}

export async function register(
  input: RegisterInput,
): Promise<{ ok: true } | { ok: false; error: string }> {
  const result = await apiClient.post('/api/auth/register', input, {
    skipAuth: true,
  })
  if (!result.ok) return { ok: false, error: result.error }
  return { ok: true }
}

export async function logout(): Promise<void> {
  try {
    await apiClient.post('/api/auth/logout', {})
  } finally {
    clearTokens()
  }
}
