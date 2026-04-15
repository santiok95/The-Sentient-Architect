'use server'

import { z } from 'zod'
import { authedActionClient } from '@/lib/action-client'
import { ingestKnowledgeSchema, publishRequestSchema } from '@/lib/schemas'

// Server-side URL: API_INTERNAL_URL is used when Next.js runs inside Docker
// (localhost inside the container doesn't reach the host).
// Falls back to NEXT_PUBLIC_API_URL for local dev (no Docker).
const BASE_URL = process.env.API_INTERNAL_URL ?? process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5291'

// ─── Ingest Knowledge ─────────────────────────────────────────────────────────

export const ingestKnowledgeAction = authedActionClient
  .schema(ingestKnowledgeSchema)
  .action(async ({ parsedInput, ctx }) => {
    const res = await fetch(`${BASE_URL}/api/v1/knowledge`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${ctx.token}`,
      },
      body: JSON.stringify(parsedInput),
    })
    if (!res.ok) {
      const err = await res.json().catch(() => ({}))
      throw new Error(err.detail ?? err.title ?? 'Error al ingestar el conocimiento')
    }
    return res.json() as Promise<{ id: string; title: string }>
  })

// ─── Delete Knowledge ─────────────────────────────────────────────────────────

const deleteKnowledgeSchema = z.object({ id: z.string().uuid() })

export const deleteKnowledgeAction = authedActionClient
  .schema(deleteKnowledgeSchema)
  .action(async ({ parsedInput, ctx }) => {
    const res = await fetch(`${BASE_URL}/api/v1/knowledge/${parsedInput.id}`, {
      method: 'DELETE',
      headers: { Authorization: `Bearer ${ctx.token}` },
    })
    if (!res.ok) {
      const err = await res.json().catch(() => ({}))
      throw new Error(err.detail ?? 'Error al eliminar el elemento')
    }
    return { deleted: true }
  })

// ─── Publish Knowledge ────────────────────────────────────────────────────────

export const publishKnowledgeAction = authedActionClient
  .schema(publishRequestSchema)
  .action(async ({ parsedInput, ctx }) => {
    const res = await fetch(
      `${BASE_URL}/api/v1/knowledge/${parsedInput.knowledgeItemId}/publish`,
      {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${ctx.token}`,
        },
        body: JSON.stringify({ reason: parsedInput.reason }),
      },
    )
    if (!res.ok) {
      const err = await res.json().catch(() => ({}))
      throw new Error(err.detail ?? 'Error al solicitar publicación')
    }
    return res.json()
  })
