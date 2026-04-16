'use server'

import { z } from 'zod'
import { authedActionClient } from '@/lib/action-client'
import { submitRepoSchema } from '@/lib/schemas'

// Server-side URL: API_INTERNAL_URL is used when Next.js runs inside Docker
// (localhost inside the container doesn't reach the host).
// Falls back to NEXT_PUBLIC_API_URL for local dev (no Docker).
const BASE_URL = process.env.API_INTERNAL_URL ?? process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5291'

// ─── Submit Repository ────────────────────────────────────────────────────────

export const submitRepoAction = authedActionClient
  .schema(submitRepoSchema)
  .action(async ({ parsedInput, ctx }) => {
    const res = await fetch(`${BASE_URL}/api/v1/repositories`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${ctx.token}`,
      },
      body: JSON.stringify({
        repositoryUrl: parsedInput.repositoryUrl,
        trust: parsedInput.trustLevel,
      }),
    })
    if (!res.ok) {
      const err = await res.json().catch(() => ({}))
      throw new Error(err.detail ?? 'Error al enviar el repositorio')
    }
    const created = await res.json() as { repositoryId: string; repositoryUrl: string }

    // Trigger background analysis (fire-and-forget — backend returns 202 immediately)
    fetch(`${BASE_URL}/api/v1/repositories/${created.repositoryId}/analyze`, {
      method: 'POST',
      headers: { Authorization: `Bearer ${ctx.token}` },
    }).catch(() => {/* best-effort — backend logs internally */})

    return created
  })

// ─── Delete Repository ────────────────────────────────────────────────────────

const deleteRepoSchema = z.object({ repositoryId: z.string().uuid() })

export const deleteRepoAction = authedActionClient
  .schema(deleteRepoSchema)
  .action(async ({ parsedInput, ctx }) => {
    const res = await fetch(
      `${BASE_URL}/api/v1/repositories/${parsedInput.repositoryId}`,
      {
        method: 'DELETE',
        headers: { Authorization: `Bearer ${ctx.token}` },
      },
    )
    if (!res.ok) {
      const err = await res.json().catch(() => ({}))
      throw new Error(err.detail ?? 'Error al eliminar el repositorio')
    }
    return { deleted: true }
  })

// ─── Reanalyze Repository ─────────────────────────────────────────────────────

const reanalyzeSchema = z.object({ repositoryId: z.string().uuid() })

export const reanalyzeAction = authedActionClient
  .schema(reanalyzeSchema)
  .action(async ({ parsedInput, ctx }) => {
    const res = await fetch(
      `${BASE_URL}/api/v1/repositories/${parsedInput.repositoryId}/analyze`,
      {
        method: 'POST',
        headers: { Authorization: `Bearer ${ctx.token}` },
      },
    )
    if (!res.ok) {
      const err = await res.json().catch(() => ({}))
      throw new Error(err.detail ?? 'Error al solicitar re-análisis')
    }
    // 202 Accepted — no body
    return {}
  })
