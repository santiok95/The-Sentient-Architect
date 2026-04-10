/**
 * Integration tests: SignalR real-time layer
 *
 * Strategy:
 * - Mock @microsoft/signalr to return a controllable EventEmitter-style stub
 * - Mock @/lib/signalr so getHubConnection returns our stub
 * - Mount components and fire events on the stub to verify UI updates
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { render, screen, act, waitFor } from '@testing-library/react'
import React from 'react'

// ─── SignalR stub ─────────────────────────────────────────────────────────────

type EventCallback = (...args: unknown[]) => void

function createHubStub() {
  const handlers: Record<string, EventCallback[]> = {}

  return {
    on(event: string, cb: EventCallback) {
      if (!handlers[event]) handlers[event] = []
      handlers[event].push(cb)
    },
    off(event: string, cb: EventCallback) {
      if (handlers[event]) {
        handlers[event] = handlers[event].filter((h) => h !== cb)
      }
    },
    onreconnecting: vi.fn(),
    onreconnected: vi.fn(),
    onclose: vi.fn(),
    state: 'Disconnected',
    // Helper: fire an event on the stub
    emit(event: string, ...args: unknown[]) {
      ;(handlers[event] ?? []).forEach((cb) => cb(...args))
    },
    // Helpers for assertions
    _handlers: handlers,
  }
}

type HubStub = ReturnType<typeof createHubStub>

let hubStubs: Record<string, HubStub> = {}

vi.mock('@/lib/signalr', () => ({
  getHubConnection: (name: string) => {
    if (!hubStubs[name]) hubStubs[name] = createHubStub()
    return hubStubs[name]
  },
  startHub: vi.fn().mockResolvedValue(undefined),
  stopHub: vi.fn(),
  getHubState: vi.fn().mockReturnValue('Connected'),
}))

// Also mock @microsoft/signalr HubConnectionState enum used in useHub
vi.mock('@microsoft/signalr', () => ({
  HubConnectionState: {
    Connected: 'Connected',
    Connecting: 'Connecting',
    Reconnecting: 'Reconnecting',
    Disconnected: 'Disconnected',
    Disconnecting: 'Disconnecting',
  },
}))

// Mock Zustand ui-store (prevent real state leaking)
vi.mock('@/store/ui-store', () => ({
  useUiStore: (selector: (s: Record<string, unknown>) => unknown) => {
    const state = {
      setHubStatus: vi.fn(),
      hubStatus: {},
      toggleConsultantPanel: vi.fn(),
    }
    return selector(state)
  },
}))

// ─── IngestProgress tests ─────────────────────────────────────────────────────

import { IngestProgress } from '@/features/brain/components/IngestProgress'

/** Flush all pending microtasks/promises so useEffect handlers register */
const flushEffects = () => act(async () => { await Promise.resolve() })

describe('IngestProgress — SignalR ingestion events', () => {
  beforeEach(() => {
    hubStubs = {}
    // Pre-create the stub so getHubConnection returns it before render
    hubStubs['ingestion'] = createHubStub()
  })

  afterEach(() => {
    vi.clearAllMocks()
  })

  it('shows nothing before any events fire', () => {
    const { container } = render(<IngestProgress />)
    expect(container.firstChild).toBeNull()
  })

  it('renders progress bar when ReceiveProgress fires', async () => {
    render(<IngestProgress />)
    await flushEffects()

    act(() => {
      hubStubs['ingestion']?.emit(
        'ReceiveProgress',
        'item-1',
        42,
        'Generating embeddings',
      )
    })

    await waitFor(() => {
      expect(screen.getByText('Generating embeddings')).toBeDefined()
    })
    expect(screen.getByText('42%')).toBeDefined()
  })

  it('shows "Completado" message when ReceiveComplete fires', async () => {
    render(<IngestProgress />)
    await flushEffects()

    act(() => {
      hubStubs['ingestion']?.emit('ReceiveProgress', 'item-1', 80, 'Indexing')
    })
    act(() => {
      hubStubs['ingestion']?.emit('ReceiveComplete', 'item-1', 15)
    })

    await waitFor(() => {
      expect(screen.getByText(/15 chunks/i)).toBeDefined()
    })
  })

  it('auto-dismisses 4 seconds after complete', async () => {
    vi.useFakeTimers()
    try {
      render(<IngestProgress />)
      await flushEffects()

      act(() => {
        hubStubs['ingestion']?.emit('ReceiveProgress', 'item-1', 50, 'Chunking')
      })
      act(() => {
        hubStubs['ingestion']?.emit('ReceiveComplete', 'item-1', 8)
      })

      // Before 4s — still visible
      act(() => { vi.advanceTimersByTime(3900) })
      expect(screen.queryByText(/chunks/i)).not.toBeNull()

      // After 4s — dismissed
      act(() => { vi.advanceTimersByTime(200) })
      expect(screen.queryByText(/chunks/i)).toBeNull()
    } finally {
      vi.useRealTimers()
    }
  })

  it('shows error message when ReceiveError fires', async () => {
    render(<IngestProgress />)
    await flushEffects()

    act(() => {
      hubStubs['ingestion']?.emit('ReceiveProgress', 'item-1', 30, 'Chunking')
    })
    act(() => {
      hubStubs['ingestion']?.emit('ReceiveError', 'item-1', 'Embedding service timeout')
    })

    await waitFor(() => {
      expect(screen.getByText(/Embedding service timeout/i)).toBeDefined()
    })
  })
})

// ─── AnalysisLiveLog tests ────────────────────────────────────────────────────

import { AnalysisLiveLog } from '@/features/guardian/components/AnalysisLiveLog'

describe('AnalysisLiveLog — SignalR analysis events', () => {
  beforeEach(() => {
    hubStubs = {}
    hubStubs['analysis'] = createHubStub()
  })

  afterEach(() => {
    vi.clearAllMocks()
  })

  it('renders nothing when repositoryId is null', () => {
    const { container } = render(
      <AnalysisLiveLog repositoryId={null} />,
    )
    expect(container.firstChild).toBeNull()
  })

  it('renders nothing before any events fire (repositoryId set)', () => {
    const { container } = render(
      <AnalysisLiveLog repositoryId="repo-123" />,
    )
    expect(container.firstChild).toBeNull()
  })

  it('shows log entry when ReceiveProgress fires for matching repo', async () => {
    render(<AnalysisLiveLog repositoryId="repo-123" />)
    await flushEffects()

    act(() => {
      hubStubs['analysis']?.emit('ReceiveProgress', 'repo-123', 25, 'Cloning repository')
    })

    await waitFor(() => {
      expect(screen.getByText(/Cloning repository/i)).toBeDefined()
    })
    expect(screen.getByText('25%')).toBeDefined()
  })

  it('ignores events for different repositoryId', async () => {
    render(<AnalysisLiveLog repositoryId="repo-123" />)
    await flushEffects()

    act(() => {
      hubStubs['analysis']?.emit('ReceiveProgress', 'repo-OTHER', 50, 'Something else')
    })

    await waitFor(() => {
      expect(screen.queryByText(/Something else/i)).toBeNull()
    })
  })

  it('accumulates multiple log entries in order', async () => {
    render(<AnalysisLiveLog repositoryId="repo-abc" />)
    await flushEffects()

    act(() => {
      hubStubs['analysis']?.emit('ReceiveProgress', 'repo-abc', 10, 'Clone')
    })
    act(() => {
      hubStubs['analysis']?.emit('ReceiveProgress', 'repo-abc', 40, 'Parse AST')
    })
    act(() => {
      hubStubs['analysis']?.emit('ReceiveProgress', 'repo-abc', 70, 'Check deps')
    })

    await waitFor(() => {
      expect(screen.getByText(/Clone/i)).toBeDefined()
      expect(screen.getByText(/Parse AST/i)).toBeDefined()
      expect(screen.getByText(/Check deps/i)).toBeDefined()
    })
  })

  it('calls onComplete with reportId when ReceiveComplete fires', async () => {
    const onComplete = vi.fn()
    render(<AnalysisLiveLog repositoryId="repo-abc" onComplete={onComplete} />)
    await flushEffects()

    act(() => {
      hubStubs['analysis']?.emit('ReceiveProgress', 'repo-abc', 90, 'Finalizing')
    })
    act(() => {
      hubStubs['analysis']?.emit('ReceiveComplete', 'repo-abc', 'report-xyz')
    })

    await waitFor(() => {
      expect(onComplete).toHaveBeenCalledWith('report-xyz')
    })
  })
})
