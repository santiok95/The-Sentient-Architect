import { createSafeActionClient } from 'next-safe-action'
import { cookies } from 'next/headers'

// ─── Base (unauthenticated) ───────────────────────────────────────────────────

export const actionClient = createSafeActionClient({
  handleServerError(e) {
    console.error('[ServerAction error]', e)
    return e instanceof Error ? e.message : 'Unexpected server error'
  },
})

// ─── Authed ───────────────────────────────────────────────────────────────────

/**
 * Authed action client.
 * Reads the JWT from the `sa_token` cookie and injects it into ctx.
 * The cookie is written client-side by lib/auth.ts on successful login.
 */
export const authedActionClient = createSafeActionClient({
  handleServerError(e) {
    console.error('[AuthedAction error]', e)
    return e instanceof Error ? e.message : 'Unexpected server error'
  },
}).use(async ({ next }) => {
  const cookieStore = await cookies()
  const token = cookieStore.get('sa_token')?.value

  if (!token) {
    throw new Error('Authentication required. Please sign in again.')
  }

  return next({ ctx: { token } })
})
