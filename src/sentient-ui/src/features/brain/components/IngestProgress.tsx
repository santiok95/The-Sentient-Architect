'use client'

/**
 * IngestProgress — real-time ingestion progress bar.
 *
 * Listens to the IngestionHub for:
 *   - ReceiveProgress(knowledgeItemId: string, percent: number, phase: string)
 *   - ReceiveComplete(knowledgeItemId: string, chunksCreated: number)
 *   - ReceiveError(knowledgeItemId: string, message: string)
 *
 * The component is unmounted (returned null) once complete or when no active job.
 */

import { useCallback, useState } from 'react'
import { CheckCircle2, AlertCircle, Loader2 } from 'lucide-react'
import { cn } from '@/lib/utils'
import { useHub } from '@/hooks/useHub'

interface ProgressState {
  knowledgeItemId: string | null
  percent: number
  phase: string
  status: 'idle' | 'running' | 'completed' | 'error'
  message?: string
  chunksCreated?: number
}

const INITIAL: ProgressState = {
  knowledgeItemId: null,
  percent: 0,
  phase: '',
  status: 'idle',
}

/**
 * Drop this component anywhere in Brain page.
 * It auto-shows/hides based on active ingestion job.
 */
export function IngestProgress() {
  const [state, setState] = useState<ProgressState>(INITIAL)

  const handleProgress = useCallback((id: string, percent: number, phase: string) => {
    setState({ knowledgeItemId: id, percent, phase, status: 'running' })
  }, [])

  const handleComplete = useCallback((id: string, chunksCreated: number) => {
    setState((s) => ({
      ...s,
      knowledgeItemId: id,
      percent: 100,
      status: 'completed',
      chunksCreated,
      phase: 'Completado',
    }))
    // Auto-dismiss after 4 seconds
    setTimeout(() => setState(INITIAL), 4000)
  }, [])

  const handleError = useCallback((id: string, message: string) => {
    setState((s) => ({ ...s, knowledgeItemId: id, status: 'error', message }))
    setTimeout(() => setState(INITIAL), 6000)
  }, [])

  useHub('ingestion', {
    handlers: {
      ReceiveProgress: handleProgress as (...args: unknown[]) => void,
      ReceiveComplete: handleComplete as (...args: unknown[]) => void,
      ReceiveError: handleError as (...args: unknown[]) => void,
    },
  })

  if (state.status === 'idle') return null

  return (
    <div
      role="status"
      aria-label="Ingestion progress"
      className={cn(
        'flex items-center gap-3 rounded-xl border px-4 py-3 text-sm transition-all',
        state.status === 'completed' && 'border-emerald-500/30 bg-emerald-500/10',
        state.status === 'error' && 'border-destructive/30 bg-destructive/10',
        state.status === 'running' && 'border-primary/30 bg-primary/10',
      )}
    >
      {/* Icon */}
      <div className="shrink-0">
        {state.status === 'running' && (
          <Loader2 className="h-4 w-4 text-primary animate-spin" />
        )}
        {state.status === 'completed' && (
          <CheckCircle2 className="h-4 w-4 text-emerald-400" />
        )}
        {state.status === 'error' && (
          <AlertCircle className="h-4 w-4 text-destructive" />
        )}
      </div>

      {/* Text */}
      <div className="flex-1 min-w-0">
        <p className={cn(
          'font-medium truncate',
          state.status === 'completed' && 'text-emerald-400',
          state.status === 'error' && 'text-destructive',
          state.status === 'running' && 'text-primary',
        )}>
          {state.status === 'error'
            ? state.message ?? 'Error al ingestar'
            : state.status === 'completed'
            ? `✓ ${state.chunksCreated} chunks generados`
            : state.phase || 'Procesando...'}
        </p>

        {/* Progress bar */}
        {state.status === 'running' && (
          <div className="mt-1.5 h-1 w-full overflow-hidden rounded-full bg-primary/20">
            <div
              className="h-full rounded-full bg-primary transition-all duration-300"
              style={{ width: `${state.percent}%` }}
            />
          </div>
        )}
      </div>

      {/* Percentage */}
      {state.status === 'running' && (
        <span className="shrink-0 font-mono text-xs text-primary">
          {state.percent}%
        </span>
      )}
    </div>
  )
}
