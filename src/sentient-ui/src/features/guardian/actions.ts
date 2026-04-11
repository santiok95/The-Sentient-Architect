'use server'

import { z } from 'zod'
import { authedActionClient } from '@/lib/action-client'
import { submitRepoSchema } from '@/lib/schemas'

const BASE_URL = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000'

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
    return res.json()
  })
