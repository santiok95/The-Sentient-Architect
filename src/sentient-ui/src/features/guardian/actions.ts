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
        // Map repositoryUrl → gitUrl per API contract
        gitUrl: parsedInput.repositoryUrl,
        trustLevel: parsedInput.trustLevel,
        notes: parsedInput.notes,
      }),
    })
    if (!res.ok) {
      const err = await res.json().catch(() => ({}))
      throw new Error(err.detail ?? 'Error al enviar el repositorio')
    }
    return res.json() as Promise<{
      knowledgeItemId: string
      repositoryInfoId: string
      processingStatus: string
    }>
  })

// ─── Reanalyze Repository ─────────────────────────────────────────────────────

const reanalyzeSchema = z.object({ knowledgeItemId: z.string().uuid() })

export const reanalyzeAction = authedActionClient
  .schema(reanalyzeSchema)
  .action(async ({ parsedInput, ctx }) => {
    const res = await fetch(
      `${BASE_URL}/api/v1/repositories/${parsedInput.knowledgeItemId}/reanalyze`,
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
