/**
 * hooks/useOfflineQueue.ts
 *
 * Watches the conversation hub status and flushes queued messages
 * automatically when connectivity is restored.
 *
 * Usage: call once inside ChatPanel, pass the executeAsync function.
 */
'use client'

import { useEffect, useRef } from 'react'
import { HubConnectionState } from '@microsoft/signalr'
import { toast } from 'sonner'
import { useUiStore, selectOfflineQueue } from '@/store/ui-store'

interface SendMessagePayload {
  conversationId: string
  content: string
  contextMode?: 'Auto' | 'RepoBound' | 'StackBound' | 'Generic'
}

interface UseOfflineQueueOptions {
  execute: (payload: SendMessagePayload) => void
}

export function useOfflineQueue({ execute }: UseOfflineQueueOptions) {
  const convState = useUiStore((s) => s.hubStatus['conversation']?.state ?? 'Unknown')
  const offlineQueue = useUiStore(selectOfflineQueue)
  const dequeueOfflineAction = useUiStore((s) => s.dequeueOfflineAction)

  const prevStateRef = useRef<string>(convState)

  useEffect(() => {
    const wasDisconnected = prevStateRef.current !== HubConnectionState.Connected
    const isNowConnected = convState === HubConnectionState.Connected

    if (wasDisconnected && isNowConnected && offlineQueue.length > 0) {
      const count = offlineQueue.length
      toast.info(`Reenviando ${count} mensaje${count > 1 ? 's' : ''} en cola...`, {
        style: { fontFamily: 'var(--font-fira-code)' },
      })

      // Flush all queued items
      offlineQueue.forEach((item) => {
        if (item.type === 'send_message') {
          execute(item.payload)
          dequeueOfflineAction(item.id)
        }
      })
    }

    prevStateRef.current = convState
  }, [convState]) // eslint-disable-line react-hooks/exhaustive-deps
}
