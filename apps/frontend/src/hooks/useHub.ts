'use client'

/**
 * useHub — lifecycle hook for a SignalR hub connection.
 *
 * Starts the hub on mount, syncs state to Zustand (for Topbar indicator),
 * and cleans up handlers (but NOT the connection) on unmount.
 *
 * Why NOT stop the connection on unmount?
 * Because the singleton must survive route navigation. Stopping it here would
 * cause a new handshake every time the user navigates back to the page.
 * The connection is only stopped explicitly on logout (lib/auth.ts).
 */

import { useCallback, useEffect, useRef } from 'react'
import { HubConnectionState } from '@microsoft/signalr'
import { getHubConnection, startHub, type HubName } from '@/lib/signalr'
import { useUiStore } from '@/store/ui-store'

type HandlerMap = Record<string, (...args: unknown[]) => void>

interface UseHubOptions {
  /** Event name → handler. Registered on mount, removed on unmount. */
  handlers?: HandlerMap
}

export function useHub(hubName: HubName, options: UseHubOptions = {}) {
  const setHubStatus = useUiStore((s) => s.setHubStatus)
  // Keep handlers in a ref so we can remove them exactly
  const handlersRef = useRef<HandlerMap>(options.handlers ?? {})
  handlersRef.current = options.handlers ?? {}

  const invoke = useCallback(
    async (method: string, ...args: unknown[]) => {
      const connection = getHubConnection(hubName)
      if (connection.state === HubConnectionState.Connected) {
        await connection.invoke(method, ...args)
      }
    },
    [hubName],
  )

  useEffect(() => {
    const connection = getHubConnection(hubName)

    // ── Sync state to Zustand ──────────────────────────────────────────────────
    function syncState() {
      setHubStatus(hubName, { state: connection.state })
    }

    connection.onreconnecting(() =>
      setHubStatus(hubName, { state: HubConnectionState.Reconnecting }),
    )
    connection.onreconnected(() =>
      setHubStatus(hubName, { state: HubConnectionState.Connected }),
    )
    connection.onclose((err) =>
      setHubStatus(hubName, {
        state: HubConnectionState.Disconnected,
        lastErrorAt: err ? new Date().toISOString() : undefined,
      }),
    )

    // ── Register event handlers ────────────────────────────────────────────────
    const registered: string[] = []
    for (const [event, handler] of Object.entries(handlersRef.current)) {
      connection.on(event, handler)
      registered.push(event)
    }

    // ── Start connection ───────────────────────────────────────────────────────
    startHub(hubName)
      .then(() => {
        syncState()
        console.log(`[useHub] ${hubName} connected`) // para probar code guardian
      })
      .catch((err) => {
        console.warn(`[useHub] Failed to start ${hubName}:`, err)
        setHubStatus(hubName, {
          state: HubConnectionState.Disconnected,
          lastErrorAt: new Date().toISOString(),
        })
      })

    return () => {
      // Remove only THIS component's handlers — do NOT stop the connection
      for (const event of registered) {
        connection.off(event, handlersRef.current[event])
      }
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [hubName])

  return { invoke }
}
