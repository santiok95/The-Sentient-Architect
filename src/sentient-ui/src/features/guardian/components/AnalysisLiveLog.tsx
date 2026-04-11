'use client'

/**
 * AnalysisLiveLog — real-time phase log for Code Guardian.
 *
 * Listens to AnalysisHub for:
 *   - ReceiveProgress(percent: number, status: string)
 *   - ReceiveComplete(reportId: string)
 *   - ReceiveError(message: string)
 *
 * Events arrive on the repository group — the component joins that group
 * via JoinRepository after the hub connects.
 */

import { useCallback, useEffect, useRef, useState } from 'react'
import { CheckCircle2, AlertCircle, Loader2, Terminal } from 'lucide-react'
import { HubConnectionState } from '@microsoft/signalr'
import { cn } from '@/lib/utils'
import { useHub } from '@/hooks/useHub'
import { getHubConnection, startHub } from '@/lib/signalr'

interface LogEntry {
  phase: string
  percent: number
  timestamp: string
  status: 'running' | 'done' | 'error'
}

interface Props {
  repositoryId: string | null
  /** Whether analysis is actively running (shows live log vs idle state) */
  isAnalyzing: boolean
  onComplete?: (reportId: string) => void
}

export function AnalysisLiveLog({ repositoryId, isAnalyzing, onComplete }: Props) {
  const [log, setLog] = useState<LogEntry[]>([])
  const [isComplete, setIsComplete] = useState(false)
  const [hasError, setHasError] = useState<string | null>(null)
  const bottomRef = useRef<HTMLDivElement>(null)

  // Reset log when repositoryId changes
  useEffect(() => {
    setLog([])
    setIsComplete(false)
    setHasError(null)
  }, [repositoryId])

  // Auto-scroll to latest entry
  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [log])

  const handleProgress = useCallback(
    (percent: number, status: string) => {
      setLog((prev) => {
        const updated = prev.map((e, i) =>
          i === prev.length - 1 && e.status === 'running'
            ? { ...e, status: 'done' as const }
            : e,
        )
        return [
          ...updated,
          {
            phase: status,
            percent,
            timestamp: new Date().toLocaleTimeString('es-AR', {
              hour: '2-digit',
              minute: '2-digit',
              second: '2-digit',
            }),
            status: 'running' as const,
          },
        ]
      })
    },
    [],
  )

  const handleComplete = useCallback(
    (reportId: string) => {
      setLog((prev) =>
        prev.map((e, i) =>
          i === prev.length - 1 ? { ...e, status: 'done' as const, percent: 100 } : e,
        ),
      )
      setIsComplete(true)
      onComplete?.(reportId)
    },
    [onComplete],
  )

  const handleError = useCallback((message: string) => {
    setLog((prev) =>
      prev.map((e, i) =>
        i === prev.length - 1 ? { ...e, status: 'error' as const } : e,
      ),
    )
    setHasError(message)
  }, [])

  useHub('analysis', {
    handlers: {
      ReceiveProgress: handleProgress as (...args: unknown[]) => void,
      ReceiveComplete: handleComplete as (...args: unknown[]) => void,
      ReceiveError: handleError as (...args: unknown[]) => void,
    },
  })

  // Join the repository SignalR group.
  // Waits for the hub to be Connected before invoking — avoids the race condition
  // where the hub is still Connecting when repositoryId first arrives.
  useEffect(() => {
    if (!repositoryId) return
    let cancelled = false

    async function join() {
      await startHub('analysis')
      if (cancelled) return
      const connection = getHubConnection('analysis')
      if (connection.state === HubConnectionState.Connected) {
        await connection.invoke('JoinRepository', repositoryId)
      }
    }

    join().catch(() => {/* hub failed to start — useHub handles the error state */})

    return () => {
      cancelled = true
      // Leave the group on cleanup (best-effort)
      const connection = getHubConnection('analysis')
      if (connection.state === HubConnectionState.Connected) {
        connection.invoke('LeaveRepository', repositoryId).catch(() => {})
      }
    }
  }, [repositoryId])

  if (!repositoryId || (!isAnalyzing && log.length === 0)) return null

  return (
    <div className="rounded-xl border border-border overflow-hidden
      bg-card dark:bg-[#0d1117]">
      {/* Terminal header bar */}
      <div className="flex items-center gap-2 px-4 py-2.5 border-b border-border
        bg-muted/70 dark:bg-[#161b22]">
        <Terminal className="h-3.5 w-3.5 text-muted-foreground/60" />
        <span className="text-xs font-mono text-muted-foreground/70 tracking-wide">
          registro de análisis
        </span>
        <span className="ml-auto text-xs font-mono text-muted-foreground/50">
          {repositoryId?.slice(0, 8)}…
        </span>
        {/* Live indicator */}
        {isAnalyzing && !isComplete && !hasError && (
          <span className="flex items-center gap-1.5 text-xs font-mono
            text-emerald-600 dark:text-emerald-400">
            <span className="h-1.5 w-1.5 rounded-full bg-emerald-500 dark:bg-emerald-400 animate-pulse" />
            en vivo
          </span>
        )}
      </div>

      {/* Log body */}
      <div className="p-4 space-y-1.5 font-mono text-xs max-h-64 overflow-y-auto bg-muted/30 dark:bg-transparent">
        {/* Waiting state */}
        {log.length === 0 && !isComplete && !hasError && (
          <div className="flex items-center gap-2.5 text-muted-foreground/60">
            <Loader2 className="h-3 w-3 animate-spin shrink-0" />
            <span>Esperando inicio del análisis…</span>
          </div>
        )}

        {/* Log entries */}
        {log.map((entry, i) => (
          <div key={i} className="flex items-baseline gap-2.5">
            {/* Status icon */}
            <span className="shrink-0 w-3.5 mt-px">
              {entry.status === 'running' && (
                <Loader2 className="h-3 w-3 text-primary animate-spin" />
              )}
              {entry.status === 'done' && (
                <CheckCircle2 className="h-3 w-3 text-emerald-500 dark:text-emerald-400" />
              )}
              {entry.status === 'error' && (
                <AlertCircle className="h-3 w-3 text-destructive" />
              )}
            </span>

            {/* Timestamp */}
            <span className="text-muted-foreground/50 shrink-0">{entry.timestamp}</span>

            {/* Percent badge */}
            <span className={cn(
              'shrink-0 tabular-nums w-8 text-right',
              entry.status === 'running' ? 'text-primary' : 'text-muted-foreground/40',
            )}>
              {entry.percent}%
            </span>

            {/* Phase message */}
            <span className={cn(
              'flex-1',
              entry.status === 'running' && 'text-foreground font-medium',
              entry.status === 'done' && 'text-muted-foreground',
              entry.status === 'error' && 'text-destructive',
            )}>
              {entry.phase}
            </span>
          </div>
        ))}

        {/* Completion / error summary */}
        {isComplete && (
          <div className="flex items-center gap-2 mt-3 pt-3 border-t border-border
            text-emerald-600 dark:text-emerald-400">
            <CheckCircle2 className="h-3.5 w-3.5 shrink-0" />
            <span>Análisis completado. Cargando reporte…</span>
          </div>
        )}
        {hasError && (
          <div className="flex items-start gap-2 mt-3 pt-3 border-t border-border text-destructive">
            <AlertCircle className="h-3.5 w-3.5 shrink-0 mt-px" />
            <span className="break-words">{hasError}</span>
          </div>
        )}

        <div ref={bottomRef} />
      </div>
    </div>
  )
}

