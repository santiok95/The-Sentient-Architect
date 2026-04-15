'use server'

import { authedActionClient } from '@/lib/action-client'
import { reviewPublishRequestSchema } from '@/lib/schemas'
// Server-side URL: API_INTERNAL_URL is used when Next.js runs inside Docker
// (localhost inside the container doesn't reach the host).
// Falls back to NEXT_PUBLIC_API_URL for local dev (no Docker).
const BASE_URL = process.env.API_INTERNAL_URL ?? process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5291'

export const reviewPublishRequestAction = authedActionClient
  .schema(reviewPublishRequestSchema)
  .action(async ({ parsedInput, ctx }) => {
    const res = await fetch(
      `${BASE_URL}/api/v1/admin/publish-requests/${parsedInput.id}`,
      {
        method: 'PATCH',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${ctx.token}`,
        },
        body: JSON.stringify({
          action: parsedInput.action,
          rejectionReason: parsedInput.rejectionReason,
        }),
      },
    )
    if (!res.ok) {
      const err = await res.json().catch(() => ({}))
      throw new Error(err.detail ?? `Error al ${parsedInput.action === 'Approve' ? 'aprobar' : 'rechazar'} la solicitud`)
    }
    return { id: parsedInput.id, action: parsedInput.action }
  })
