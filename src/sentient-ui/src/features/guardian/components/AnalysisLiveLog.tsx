'use client'

/**
 * AnalysisLiveLog — real-time phase log for Code Guardian.
 *
 * Listens to AnalysisHub for:
 *   - ReceiveProgress(repositoryId: string, percent: number, status: string)
 *   - ReceiveComplete(repositoryId: string, reportId: string)
 *   - ReceiveError(repositoryId: string, message: string)
 *
 * Feed `repositoryId` to watch a specific repo. Pass null to show nothing.
 */

import { useCallback, useState } from 'react'
import { CheckCircle2, AlertCircle, Circle, Loader2 } from 'lucide-react'
import { cn } from '@/lib/utils'
import { useHub } from '@/hooks/useHub'

interface LogEntry {
  phase: string
  percent: number
  timestamp: string
  status: 'running' | 'done' | 'error'
}

interface Props {
  repositoryId: string | null
  onComplete?: (reportId: string) => void
}

export function AnalysisLiveLog({ repositoryId, onComplete }: Props) {
  const [log, setLog] = useState<LogEntry[]>([])
  const [isComplete, setIsComplete] = useState(false)
  const [hasError, setHasError] = useState<string | null>(null)

  const handleProgress = useCallback(
    (repoId: string, percent: number, status: string) => {
      if (repoId !== repositoryId) return
      setLog((prev) => {
        // Mark previous entry as done, add new running entry
        const updated = prev.map((e, i) =>
          i === prev.length - 1 && e.status === 'running' ? { ...e, status: 'done' as const } : e,
        )
        return [
          ...updated,
          {
            phase: status,
            percent,
            timestamp: new Date().toLocaleTimeString('es-AR', { hour: '2-digit', minute: '2-digit', second: '2-digit' }),
            status: 'running',
          },
        ]
      })
    },
    [repositoryId],
  )

  const handleComplete = useCallback(
    (repoId: string, reportId: string) => {
      if (repoId !== repositoryId) return
      setLog((prev) =>
        prev.map((e, i) =>
          i === prev.length - 1 ? { ...e, status: 'done', percent: 100 } : e,
        ),
      )
      setIsComplete(true)
      onComplete?.(reportId)
    },
    [repositoryId, onComplete],
  )

  const handleError = useCallback(
    (repoId: string, message: string) => {
      if (repoId !== repositoryId) return
      setLog((prev) =>
        prev.map((e, i) =>
          i === prev.length - 1 ? { ...e, status: 'error' } : e,
        ),
      )
      setHasError(message)
    },
    [repositoryId],
  )

  useHub('analysis', {
    handlers: {
      ReceiveProgress: handleProgress as (...args: unknown[]) => void,
      ReceiveComplete: handleComplete as (...args: unknown[]) => void,
      ReceiveError: handleError as (...args: unknown[]) => void,
    },
  })

  if (!repositoryId || log.length === 0) return null

  return (
    <div className="rounded-xl border border-border bg-card p-4 space-y-1 font-mono text-xs">
      <p className="text-[10px] font-medium uppercase tracking-widest text-muted-foreground mb-3">
        Analysis Log
      </p>

      {log.map((entry, i) => (
        <div key={i} className="flex items-center gap-2.5">
          {/* Status icon */}
          <span className="shrink-0 w-3.5">
            {entry.status === 'running' && (
              <Loader2 className="h-3 w-3 text-primary animate-spin" />
            )}
            {entry.status === 'done' && (
              <CheckCircle2 className="h-3 w-3 text-emerald-400" />
            )}
            {entry.status === 'error' && (
              <AlertCircle className="h-3 w-3 text-destructive" />
            )}
          </span>

          {/* Timestamp */}
          <span className="text-muted-foreground/50 shrink-0">{entry.timestamp}</span>

          {/* Phase */}
          <span className={cn(
            'flex-1 truncate',
            entry.status === 'running' && 'text-primary',
            entry.status === 'done' && 'text-foreground/70',
            entry.status === 'error' && 'text-destructive',
          )}>
            {entry.phase}
          </span>

          {/* Percent */}
          <span className={cn(
            'shrink-0 tabular-nums',
            entry.status === 'running' ? 'text-primary' : 'text-muted-foreground/50',
          )}>
            {entry.percent}%
          </span>
        </div>
      ))}

      {/* Summary row */}
      {isComplete && (
        <div className="mt-3 pt-3 border-t border-border flex items-center gap-2 text-emerald-400">
          <CheckCircle2 className="h-3.5 w-3.5" />
          <span>Análisis completado</span>
        </div>
      )}
      {hasError && (
        <div className="mt-3 pt-3 border-t border-border flex items-center gap-2 text-destructive">
          <AlertCircle className="h-3.5 w-3.5" />
          <span className="truncate">{hasError}</span>
        </div>
      )}
    </div>
  )
}
