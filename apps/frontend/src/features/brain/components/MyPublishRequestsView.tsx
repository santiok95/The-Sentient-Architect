'use client'

import { useState } from 'react'
import { CheckCircle2, XCircle, Clock, Info } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'
import { useMyPublishRequests } from '../hooks/useMyPublishRequests'
import type { MyPublishRequest } from '../hooks/useMyPublishRequests'

// ─── Status config ────────────────────────────────────────────────────────────

const STATUS_CONFIG = {
  Pending:  { label: 'Pendiente', icon: Clock,        className: 'border-amber-500/40 bg-amber-500/10 text-amber-400' },
  Approved: { label: 'Aprobado',  icon: CheckCircle2, className: 'border-emerald-500/40 bg-emerald-500/10 text-emerald-400' },
  Rejected: { label: 'Rechazado', icon: XCircle,      className: 'border-red-500/40 bg-red-500/10 text-red-400' },
} as const

const TYPE_LABELS: Record<string, string> = {
  Article:             'Artículo',
  Note:                'Nota',
  Documentation:       'Doc',
  Repository:          'Repo',
  RepositoryReference: 'Repo',
}

function SkeletonRow() {
  return (
    <tr className="border-b border-border/50">
      <td className="py-3.5 px-4"><Skeleton className="h-4 w-52 mb-1" /><Skeleton className="h-3 w-32 mt-1" /></td>
      <td className="py-3.5 px-4"><Skeleton className="h-3 w-20" /></td>
      <td className="py-3.5 px-4"><Skeleton className="h-5 w-24 rounded-full" /></td>
    </tr>
  )
}

function RequestRow({ request }: { request: MyPublishRequest }) {
  const config = STATUS_CONFIG[request.status]
  const StatusIcon = config.icon
  const typeLabel = TYPE_LABELS[request.knowledgeItemType] ?? request.knowledgeItemType
  const date = new Date(request.createdAt).toLocaleDateString('es-AR', {
    day: '2-digit', month: 'short', year: '2-digit',
  })

  return (
    <tr className="border-b border-border/50 hover:bg-muted/30 transition-colors">
      <td className="py-3.5 px-4">
        <div className="flex items-start gap-2">
          <Badge variant="outline" className="mt-0.5 shrink-0 text-[10px] font-mono px-1.5 py-0">
            {typeLabel}
          </Badge>
          <div className="min-w-0">
            <p className="font-mono text-sm font-medium text-foreground truncate max-w-[280px]">
              {request.knowledgeItemTitle}
            </p>
            {request.status === 'Rejected' && request.rejectionReason && (
              <p className="mt-0.5 text-xs text-destructive/80 line-clamp-1 max-w-[280px]">
                Motivo: {request.rejectionReason}
              </p>
            )}
          </div>
        </div>
      </td>
      <td className="py-3.5 px-4">
        <span className="font-mono text-xs text-muted-foreground">{date}</span>
      </td>
      <td className="py-3.5 px-4">
        <Badge variant="outline" className={cn('gap-1 text-xs font-mono', config.className)}>
          <StatusIcon className="h-3 w-3" />
          {config.label}
        </Badge>
      </td>
    </tr>
  )
}

export function MyPublishRequestsView() {
  const [statusFilter, setStatusFilter] = useState('')
  const { data, isLoading } = useMyPublishRequests()

  const requests = (data?.items ?? []).filter(
    (r) => !statusFilter || r.status === statusFilter,
  )

  const pendingCount = (data?.items ?? []).filter((r) => r.status === 'Pending').length

  return (
    <div className="space-y-4">
      {/* Info banner */}
      <div className="flex items-start gap-2.5 rounded-lg border border-border bg-muted/30 px-4 py-3">
        <Info className="h-4 w-4 text-muted-foreground shrink-0 mt-0.5" />
        <p className="text-sm text-muted-foreground">
          Cuando ingresás contenido, se crea automáticamente una solicitud de publicación que un administrador debe aprobar para compartirlo con el equipo.
        </p>
      </div>

      {/* Filter tabs */}
      <div className="flex items-center gap-2">
        {(['', 'Pending', 'Approved', 'Rejected'] as const).map((s) => (
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
            {s === '' ? 'Todas' : STATUS_CONFIG[s]?.label}
          </button>
        ))}
      </div>

      {/* Table */}
      <div className="rounded-xl border border-border overflow-hidden">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-border bg-muted/50">
              <th className="py-2.5 px-4 text-left text-xs font-mono font-medium text-muted-foreground">Artículo</th>
              <th className="py-2.5 px-4 text-left text-xs font-mono font-medium text-muted-foreground">Fecha</th>
              <th className="py-2.5 px-4 text-left text-xs font-mono font-medium text-muted-foreground">Estado</th>
            </tr>
          </thead>
          <tbody>
            {isLoading
              ? Array.from({ length: 4 }).map((_, i) => <SkeletonRow key={i} />)
              : requests.length === 0
                ? (
                  <tr>
                    <td colSpan={3} className="py-12 text-center text-sm text-muted-foreground">
                      {statusFilter
                        ? 'No hay solicitudes con este estado'
                        : 'Todavía no tenés solicitudes de publicación'}
                    </td>
                  </tr>
                )
                : requests.map((r) => <RequestRow key={r.id} request={r} />)
            }
          </tbody>
        </table>
      </div>
    </div>
  )
}
