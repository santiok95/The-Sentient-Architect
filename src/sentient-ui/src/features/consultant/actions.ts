'use server'

import { authedActionClient } from '@/lib/action-client'
import { createConversationSchema, sendMessageSchema } from '@/lib/schemas'
import { z } from 'zod'

const BASE_URL = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000'

// ─── Create Conversation ─────────────────────────────────────────────────────

export const createConversationAction = authedActionClient
  .schema(createConversationSchema)
  .action(async ({ parsedInput, ctx }) => {
    const res = await fetch(`${BASE_URL}/api/v1/conversations`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${ctx.token}`,
      },
      body: JSON.stringify({
        title: parsedInput.title ?? 'Nueva consulta',
        agentType: parsedInput.agentType ?? 'Knowledge',
        ...(parsedInput.activeRepositoryId && { activeRepositoryId: parsedInput.activeRepositoryId }),
      }),
    })
    if (!res.ok) {
      const err = await res.json().catch(() => ({}))
      throw new Error(err.detail ?? 'Error al crear la conversación')
    }
    const data = await res.json() as { conversationId?: string; id?: string; title: string }
    const id = data.conversationId ?? data.id ?? ''
    return { id, title: data.title, status: 'Active' }
  })

// ─── Send Message ─────────────────────────────────────────────────────────────

export const sendMessageAction = authedActionClient
  .schema(sendMessageSchema)
  .action(async ({ parsedInput, ctx }) => {
    const { conversationId, content, contextMode, preferredStack, activeRepositoryId } = parsedInput
    const res = await fetch(`${BASE_URL}/api/v1/conversations/${conversationId}/chat`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${ctx.token}`,
      },
      body: JSON.stringify({
        message: content,
        ...(contextMode && { contextMode }),
        ...(preferredStack && { preferredStack }),
        ...(activeRepositoryId && { activeRepositoryId }),
      }),
    })
    if (!res.ok) {
      const err = await res.json().catch(() => ({}))
      throw new Error(err.detail ?? 'Error al enviar el mensaje')
    }
    return res.json()
  })

// ─── Archive Conversation ─────────────────────────────────────────────────────

const archiveSchema = z.object({ id: z.string().uuid() })
const deleteSchema = z.object({ id: z.string().uuid() })

export const archiveConversationAction = authedActionClient
  .schema(archiveSchema)
  .action(async ({ parsedInput, ctx }) => {
    const res = await fetch(`${BASE_URL}/api/v1/conversations/${parsedInput.id}/archive`, {
      method: 'PATCH',
      headers: { Authorization: `Bearer ${ctx.token}` },
    })
    if (!res.ok) {
      const err = await res.json().catch(() => ({}))
      throw new Error(err.detail ?? 'Error al archivar la conversación')
    }
    return { archived: true }
  })

// ─── Delete Conversation ──────────────────────────────────────────────────────

export const deleteConversationAction = authedActionClient
  .schema(deleteSchema)
  .action(async ({ parsedInput, ctx }) => {
    const res = await fetch(`${BASE_URL}/api/v1/conversations/${parsedInput.id}`, {
      method: 'DELETE',
      headers: { Authorization: `Bearer ${ctx.token}` },
    })
    if (!res.ok) {
      const err = await res.json().catch(() => ({}))
      throw new Error(err.detail ?? 'Error al eliminar la conversación')
    }
    return { deleted: true }
  })
