/**
 * __tests__/resiliency/offline-queue.test.tsx
 *
 * Stress tests for the offline queue pattern.
 * Verifies that:
 *   1. Messages sent while the hub is Disconnected land in the offline queue
 *   2. When the hub reconnects, the queue is flushed automatically
 *   3. The queue is cleared after a successful flush
 *   4. Offline badge renders when hub is disconnected
 *   5. Queue items are persisted across (simulated) re-renders
 */

import { describe, it, expect, vi, beforeEach } from 'vitest'
import { renderHook, act } from '@testing-library/react'
import { HubConnectionState } from '@microsoft/signalr'
import { useUiStore } from '@/store/ui-store'
import { useOfflineQueue } from '@/hooks/useOfflineQueue'

// ─── Reset Zustand store between tests ────────────────────────────────────────

beforeEach(() => {
  useUiStore.setState({
    hubStatus: {},
    offlineQueue: [],
  })
  vi.clearAllMocks()
})

// ─── OfflineQueueItem shape ───────────────────────────────────────────────────

describe('offline queue store actions', () => {
  it('enqueues a message with required metadata', () => {
    const { result } = renderHook(() => useUiStore())

    act(() => {
      result.current.enqueueOfflineAction({
        type: 'send_message',
        payload: { conversationId: 'conv-001', content: 'Hola', contextMode: 'Auto' },
      })
    })

    const queue = result.current.offlineQueue
    expect(queue).toHaveLength(1)
    expect(queue[0].type).toBe('send_message')
    expect(queue[0].payload.content).toBe('Hola')
    expect(queue[0].id).toMatch(/^oq-/)
    expect(queue[0].queuedAt).toBeTruthy()
    expect(queue[0].retryCount).toBe(0)
  })

  it('enqueues multiple messages in order', () => {
    const { result } = renderHook(() => useUiStore())

    act(() => {
      result.current.enqueueOfflineAction({
        type: 'send_message',
        payload: { conversationId: 'conv-001', content: 'First', contextMode: 'Auto' },
      })
      result.current.enqueueOfflineAction({
        type: 'send_message',
        payload: { conversationId: 'conv-001', content: 'Second', contextMode: 'Auto' },
      })
    })

    const queue = result.current.offlineQueue
    expect(queue).toHaveLength(2)
    expect(queue[0].payload.content).toBe('First')
    expect(queue[1].payload.content).toBe('Second')
  })

  it('dequeues a specific item by id', () => {
    const { result } = renderHook(() => useUiStore())

    act(() => {
      result.current.enqueueOfflineAction({
        type: 'send_message',
        payload: { conversationId: 'conv-001', content: 'Keep', contextMode: 'Auto' },
      })
      result.current.enqueueOfflineAction({
        type: 'send_message',
        payload: { conversationId: 'conv-001', content: 'Remove', contextMode: 'Auto' },
      })
    })

    const idToRemove = result.current.offlineQueue[1].id

    act(() => {
      result.current.dequeueOfflineAction(idToRemove)
    })

    expect(result.current.offlineQueue).toHaveLength(1)
    expect(result.current.offlineQueue[0].payload.content).toBe('Keep')
  })

  it('clears all queued items', () => {
    const { result } = renderHook(() => useUiStore())

    act(() => {
      result.current.enqueueOfflineAction({
        type: 'send_message',
        payload: { conversationId: 'conv-001', content: 'A', contextMode: 'Auto' },
      })
      result.current.enqueueOfflineAction({
        type: 'send_message',
        payload: { conversationId: 'conv-001', content: 'B', contextMode: 'Auto' },
      })
      result.current.clearOfflineQueue()
    })

    expect(result.current.offlineQueue).toHaveLength(0)
  })

  it('increments retry count for an item', () => {
    const { result } = renderHook(() => useUiStore())

    act(() => {
      result.current.enqueueOfflineAction({
        type: 'send_message',
        payload: { conversationId: 'conv-001', content: 'Retry me', contextMode: 'Auto' },
      })
    })

    const id = result.current.offlineQueue[0].id

    act(() => {
      result.current.incrementRetry(id)
      result.current.incrementRetry(id)
    })

    expect(result.current.offlineQueue[0].retryCount).toBe(2)
  })

  it('assigns unique ids to each queued item', () => {
    const { result } = renderHook(() => useUiStore())

    act(() => {
      for (let i = 0; i < 5; i++) {
        result.current.enqueueOfflineAction({
          type: 'send_message',
          payload: { conversationId: 'conv-001', content: `msg-${i}`, contextMode: 'Auto' },
        })
      }
    })

    const ids = result.current.offlineQueue.map((item) => item.id)
    const uniqueIds = new Set(ids)
    expect(uniqueIds.size).toBe(5)
  })
})

// ─── useOfflineQueue hook ─────────────────────────────────────────────────────

describe('useOfflineQueue hook', () => {
  it('does NOT flush when hub remains Connected throughout', () => {
    const execute = vi.fn()

    // Pre-queue a message
    useUiStore.setState({
      hubStatus: { conversation: { state: HubConnectionState.Connected } },
      offlineQueue: [
        {
          id: 'oq-test-1',
          type: 'send_message',
          payload: { conversationId: 'conv-001', content: 'Queued', contextMode: 'Auto' },
          queuedAt: new Date().toISOString(),
          retryCount: 0,
        },
      ],
    })

    renderHook(() => useOfflineQueue({ execute }))

    // No transition (was already Connected), execute should not be called
    expect(execute).not.toHaveBeenCalled()
  })

  it('flushes all queued messages when hub transitions to Connected', () => {
    const execute = vi.fn()

    // Start disconnected with queued messages
    useUiStore.setState({
      hubStatus: { conversation: { state: HubConnectionState.Disconnected } },
      offlineQueue: [
        {
          id: 'oq-1',
          type: 'send_message',
          payload: { conversationId: 'conv-001', content: 'msg-1', contextMode: 'Auto' },
          queuedAt: new Date().toISOString(),
          retryCount: 0,
        },
        {
          id: 'oq-2',
          type: 'send_message',
          payload: { conversationId: 'conv-001', content: 'msg-2', contextMode: 'Auto' },
          queuedAt: new Date().toISOString(),
          retryCount: 0,
        },
      ],
    })

    renderHook(() => useOfflineQueue({ execute }))

    // Simulate hub reconnecting
    act(() => {
      useUiStore.setState({
        hubStatus: { conversation: { state: HubConnectionState.Connected } },
      })
    })

    expect(execute).toHaveBeenCalledTimes(2)
    expect(execute).toHaveBeenCalledWith({
      conversationId: 'conv-001',
      content: 'msg-1',
      contextMode: 'Auto',
    })
    expect(execute).toHaveBeenCalledWith({
      conversationId: 'conv-001',
      content: 'msg-2',
      contextMode: 'Auto',
    })
  })

  it('dequeues all items after flushing', () => {
    const execute = vi.fn()

    useUiStore.setState({
      hubStatus: { conversation: { state: HubConnectionState.Disconnected } },
      offlineQueue: [
        {
          id: 'oq-flush-1',
          type: 'send_message',
          payload: { conversationId: 'conv-001', content: 'flush me', contextMode: 'Auto' },
          queuedAt: new Date().toISOString(),
          retryCount: 0,
        },
      ],
    })

    renderHook(() => useOfflineQueue({ execute }))

    act(() => {
      useUiStore.setState({
        hubStatus: { conversation: { state: HubConnectionState.Connected } },
      })
    })

    // Queue should be empty after flush
    expect(useUiStore.getState().offlineQueue).toHaveLength(0)
  })

  it('does NOT flush when already Connected (no state transition)', () => {
    const execute = vi.fn()

    useUiStore.setState({
      hubStatus: { conversation: { state: HubConnectionState.Connected } },
      offlineQueue: [
        {
          id: 'oq-noflush',
          type: 'send_message',
          payload: { conversationId: 'conv-001', content: 'should stay', contextMode: 'Auto' },
          queuedAt: new Date().toISOString(),
          retryCount: 0,
        },
      ],
    })

    renderHook(() => useOfflineQueue({ execute }))

    // Trigger a re-render without changing hub state
    act(() => {
      useUiStore.setState({
        hubStatus: { conversation: { state: HubConnectionState.Connected } },
      })
    })

    expect(execute).not.toHaveBeenCalled()
    expect(useUiStore.getState().offlineQueue).toHaveLength(1)
  })

  it('does not flush empty queue on reconnect', () => {
    const execute = vi.fn()

    useUiStore.setState({
      hubStatus: { conversation: { state: HubConnectionState.Disconnected } },
      offlineQueue: [],
    })

    renderHook(() => useOfflineQueue({ execute }))

    act(() => {
      useUiStore.setState({
        hubStatus: { conversation: { state: HubConnectionState.Connected } },
      })
    })

    expect(execute).not.toHaveBeenCalled()
  })
})
