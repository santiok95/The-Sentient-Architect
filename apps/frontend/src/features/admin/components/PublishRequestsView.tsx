'use client'

import { useOptimistic, useTransition, useState } from 'react'
import { useAction } from 'next-safe-action/hooks'
import { CheckCircle2, XCircle, Clock, AlertCircle, ChevronDown } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/dialog'
import { Textarea } from '@/components/ui/textarea'
import { Label } from '@/components/ui/label'
import { toast } from 'sonner'
import { cn } from '@/lib/utils'
import { usePublishRequests, useInvalidatePublishRequests } from '../hooks/usePublishRequests'
import { reviewPublishRequestAction } from '../actions'
import type { PublishRequest, PublishRequestStatus } from '@/lib/api.types'

// ─── Status config ────────────────────────────────────────────────────────────

const STATUS_CONFIG: Record<PublishRequestStatus, {
  label: string
  icon: React.ElementType
  className: string
}> = {
  Pending:  { label: 'Pendiente', icon: Clock,        className: 'border-amber-500/40 bg-amber-500/10 text-amber-400' },
  Approved: { label: 'Aprobado',  icon: CheckCircle2, className: 'border-emerald-500/40 bg-emerald-500/10 text-emerald-400' },
  Rejected: { label: 'Rechazado', icon: XCircle,      className: 'border-red-500/40 bg-red-500/10 text-red-400' },
}

const TYPE_LABELS: Record<string, string> = {
  Article:       'Artículo',
  Note:          'Nota',
  Documentation: 'Doc',
  Repository:    'Repo',
}

// ─── Reject Dialog ────────────────────────────────────────────────────────────

interface RejectDialogProps {
  open: boolean
  onClose: () => void
  onConfirm: (reason: string) => void
  isPending: boolean
}

function RejectDialog({ open, onClose, onConfirm, isPending }: RejectDialogProps) {
  const [reason, setReason] = useState('')
  const canSubmit = reason.trim().length >= 5 && !isPending

  return (
    <Dialog open={open} onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle className="font-mono">Rechazar solicitud</DialogTitle>
        </DialogHeader>
        <div className="space-y-3 py-2">
          <Label htmlFor="reject-reason" className="text-sm text-muted-foreground">
            Motivo del rechazo <span className="text-destructive">*</span>
          </Label>
          <Textarea
            id="reject-reason"
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder="Indicá por qué se rechaza esta solicitud..."
            className="font-mono text-sm resize-none"
            rows={3}
          />
          {reason.length > 0 && reason.trim().length < 5 && (
            <p className="flex items-center gap-1.5 text-xs text-destructive font-mono">
              <AlertCircle className="h-3 w-3" />
              Mínimo 5 caracteres
            </p>
          )}
        </div>
        <DialogFooter>
          <Button variant="ghost" onClick={onClose} disabled={isPending}>Cancelar</Button>
          <Button
            variant="destructive"
            onClick={() => onConfirm(reason)}
            disabled={!canSubmit}
          >
            {isPending ? 'Rechazando...' : 'Rechazar'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

// ─── Request Row ──────────────────────────────────────────────────────────────

interface RequestRowProps {
  request: PublishRequest
  onApprove: (id: string) => void
  onReject: (id: string) => void
  isPending: boolean
}

function RequestRow({ request, onApprove, onReject, isPending }: RequestRowProps) {
  const config = STATUS_CONFIG[request.status]
  const StatusIcon = config.icon
  const typeLabel = TYPE_LABELS[request.knowledgeItem.type] ?? request.knowledgeItem.type
  const date = new Date(request.createdAt).toLocaleDateString('es-AR', {
    day: '2-digit', month: 'short', year: '2-digit',
  })
  const isPendingRow = request.status === 'Pending'

  return (
    <tr className={cn(
      'border-b border-border/50 transition-all duration-300',
      'hover:bg-muted/30',
      isPending && isPendingRow && 'opacity-60',
    )}>
      {/* Article */}
      <td className="py-3.5 px-4">
        <div className="flex items-start gap-2">
          <Badge variant="outline" className="mt-0.5 shrink-0 text-[10px] font-mono px-1.5 py-0">
            {typeLabel}
          </Badge>
          <div className="min-w-0">
            <p className="font-mono text-sm font-medium text-foreground truncate max-w-[260px]">
              {request.knowledgeItem.title}
            </p>
            {request.knowledgeItem.summary && (
              <p className="mt-0.5 text-xs text-muted-foreground line-clamp-1 max-w-[260px]">
                {request.knowledgeItem.summary}
              </p>
            )}
          </div>
        </div>
      </td>

      {/* Requested By */}
      <td className="py-3.5 px-4">
        <p className="text-sm font-medium">{request.requestedBy.displayName}</p>
        {request.requestReason && (
          <p className="mt-0.5 text-xs text-muted-foreground line-clamp-1 max-w-[180px] italic">
            "{request.requestReason}"
          </p>
        )}
      </td>

      {/* Date */}
      <td className="py-3.5 px-4">
        <span className="font-mono text-xs text-muted-foreground">{date}</span>
      </td>

      {/* Status */}
      <td className="py-3.5 px-4">
        <Badge variant="outline" className={cn('gap-1 text-xs font-mono', config.className)}>
          <StatusIcon className="h-3 w-3" />
          {config.label}
        </Badge>
      </td>

      {/* Actions */}
      <td className="py-3.5 px-4 text-right">
        {isPendingRow && (
          <div className="flex items-center justify-end gap-2">
            <Button
              size="sm"
              variant="outline"
              className="h-7 text-xs gap-1 border-emerald-500/40 text-emerald-400 hover:bg-emerald-500/10 hover:text-emerald-300"
              onClick={() => onApprove(request.id)}
              disabled={isPending}
              aria-label="Aprobar solicitud"
            >
              <CheckCircle2 className="h-3 w-3" />
              Aprobar
            </Button>
            <Button
              size="sm"
              variant="outline"
              className="h-7 text-xs gap-1 border-red-500/40 text-red-400 hover:bg-red-500/10 hover:text-red-300"
              onClick={() => onReject(request.id)}
              disabled={isPending}
              aria-label="Rechazar solicitud"
            >
              <XCircle className="h-3 w-3" />
              Rechazar
            </Button>
          </div>
        )}
      </td>
    </tr>
  )
}

// ─── Skeleton rows ────────────────────────────────────────────────────────────

function SkeletonRow() {
  return (
    <tr className="border-b border-border/50">
      <td className="py-3.5 px-4"><Skeleton className="h-4 w-52 mb-1.5" /><Skeleton className="h-3 w-64" /></td>
      <td className="py-3.5 px-4"><Skeleton className="h-4 w-32" /></td>
      <td className="py-3.5 px-4"><Skeleton className="h-3 w-20" /></td>
      <td className="py-3.5 px-4"><Skeleton className="h-5 w-24 rounded-full" /></td>
      <td className="py-3.5 px-4"><Skeleton className="h-7 w-32 ml-auto rounded-md" /></td>
    </tr>
  )
}

// ─── Main Component ───────────────────────────────────────────────────────────

type OptimisticUpdate = { id: string; status: PublishRequestStatus }

export function PublishRequestsView() {
  const [statusFilter, setStatusFilter] = useState<string>('Pending')
  const [rejectTarget, setRejectTarget] = useState<string | null>(null)
  const [, startTransition] = useTransition()
  const invalidate = useInvalidatePublishRequests()

  const { data, isLoading } = usePublishRequests(statusFilter || undefined)
  const requests: PublishRequest[] = data?.items ?? []

  // ── useOptimistic: zero-latency row updates ────────────────────────────────
  const [optimisticRequests, updateOptimistic] = useOptimistic(
    requests,
    (state: PublishRequest[], update: OptimisticUpdate) =>
      state.map((r) => r.id === update.id ? { ...r, status: update.status } : r),
  )

  const { executeAsync, isPending } = useAction(reviewPublishRequestAction)

  async function handleApprove(id: string) {
    startTransition(async () => {
      updateOptimistic({ id, status: 'Approved' })
      const result = await executeAsync({ id, action: 'Approve' })
      if (result?.serverError) {
        toast.error(result.serverError, {
          style: { fontFamily: 'var(--font-fira-code)' },
        })
      } else {
        toast.success('Solicitud aprobada', {
          description: 'El contenido ahora es público',
          style: { fontFamily: 'var(--font-fira-code)' },
        })
        invalidate()
      }
    })
  }

  async function handleRejectConfirm(reason: string) {
    if (!rejectTarget) return
    const id = rejectTarget
    setRejectTarget(null)

    startTransition(async () => {
      updateOptimistic({ id, status: 'Rejected' })
      const result = await executeAsync({ id, action: 'Reject', rejectionReason: reason })
      if (result?.serverError) {
        toast.error(result.serverError, {
          style: { fontFamily: 'var(--font-fira-code)' },
        })
      } else {
        toast.info('Solicitud rechazada', {
          style: { fontFamily: 'var(--font-fira-code)' },
        })
        invalidate()
      }
    })
  }

  const pendingCount = requests.filter((r) => r.status === 'Pending').length

  return (
    <div className="space-y-4">
      {/* Filter tabs */}
      <div className="flex items-center gap-2">
        {['Pending', 'Approved', 'Rejected', ''].map((s) => (
          <button
            key={s}
            onClick={() => setStatusFilter(s)}
            className={cn(
              'flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-mono transition-colors',
              statusFilter === s
                ? 'bg-primary/20 text-primary border border-primary/30'
                : 'text-muted-foreground hover:text-foreground hover:bg-muted/50',
            )}
          >
            {s === 'Pending' && pendingCount > 0 && (
              <span className="flex h-4 w-4 items-center justify-center rounded-full bg-amber-500 text-[10px] font-bold text-black">
                {pendingCount}
              </span>
            )}
            {s === '' ? 'Todas' : STATUS_CONFIG[s as PublishRequestStatus]?.label ?? 'Todas'}
          </button>
        ))}
      </div>

      {/* Table */}
      <div className="rounded-xl border border-border overflow-hidden">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-border bg-muted/50">
              <th className="py-2.5 px-4 text-left text-xs font-mono font-medium text-muted-foreground">Artículo</th>
              <th className="py-2.5 px-4 text-left text-xs font-mono font-medium text-muted-foreground">Solicitado por</th>
              <th className="py-2.5 px-4 text-left text-xs font-mono font-medium text-muted-foreground">Fecha</th>
              <th className="py-2.5 px-4 text-left text-xs font-mono font-medium text-muted-foreground">Estado</th>
              <th className="py-2.5 px-4 text-right text-xs font-mono font-medium text-muted-foreground">Acciones</th>
            </tr>
          </thead>
          <tbody>
            {isLoading
              ? Array.from({ length: 5 }).map((_, i) => <SkeletonRow key={i} />)
              : optimisticRequests.length === 0
                ? (
                  <tr>
                    <td colSpan={5} className="py-12 text-center text-sm text-muted-foreground">
                      No hay solicitudes con este estado
                    </td>
                  </tr>
                )
                : optimisticRequests.map((r) => (
                  <RequestRow
                    key={r.id}
                    request={r}
                    onApprove={handleApprove}
                    onReject={(id) => setRejectTarget(id)}
                    isPending={isPending}
                  />
                ))}
          </tbody>
        </table>
      </div>

      <RejectDialog
        open={rejectTarget !== null}
        onClose={() => setRejectTarget(null)}
        onConfirm={handleRejectConfirm}
        isPending={isPending}
      />
    </div>
  )
}
