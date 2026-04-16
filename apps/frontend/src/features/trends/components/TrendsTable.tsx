'use client'

import { useState } from 'react'
import { TrendingUp, TrendingDown, Minus, ArrowUpRight, RefreshCw, Filter, Star, ExternalLink, ChevronLeft, ChevronRight } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { toast } from 'sonner'
import { cn } from '@/lib/utils'
import { apiClient } from '@/lib/api-client'
import { useTrends, TREND_CATEGORIES, TRACTION_LEVELS } from '../hooks/useTrends'
import type { Trend, TractionLevel } from '@/lib/api.types'

// ─── Category labels ──────────────────────────────────────────────────────────

const CATEGORY_LABELS: Record<string, string> = {
  Framework:    'Framework',
  Language:     'Lenguaje',
  Tool:         'Herramienta',
  Pattern:      'Patrón',
  Platform:     'Plataforma',
  Library:      'Librería',
  BestPractice: 'Buenas Prácticas',
  Innovation:   'Innovación',
  Architecture: 'Arquitectura',
  DevOps:       'DevOps',
  Testing:      'Testing',
}

// ─── Traction level config ─────────────────────────────────────────────────────

const TRACTION_CONFIG: Record<TractionLevel, {
  label: string
  icon: React.ElementType
  className: string
  barColor: string
}> = {
  Emerging:   { label: 'Emergente',  icon: ArrowUpRight, className: 'border-sky-500/40 bg-sky-500/10 text-sky-400',       barColor: 'bg-sky-500' },
  Growing:    { label: 'Creciendo',  icon: TrendingUp,   className: 'border-violet-500/40 bg-violet-500/10 text-violet-400', barColor: 'bg-violet-500' },
  Mainstream: { label: 'Mainstream', icon: Minus,        className: 'border-emerald-500/40 bg-emerald-500/10 text-emerald-400', barColor: 'bg-emerald-500' },
  Declining:  { label: 'Bajando',    icon: TrendingDown, className: 'border-red-500/40 bg-red-500/10 text-red-400',          barColor: 'bg-red-500' },
}

// ─── Score Bar ──────────────────────────────────────────────────────────────

function ScoreBar({ score, tractionLevel }: { score: number; tractionLevel: TractionLevel }) {
  const { barColor } = TRACTION_CONFIG[tractionLevel]
  return (
    <div className="flex items-center gap-2.5 min-w-[120px]">
      <div className="flex-1 h-1.5 rounded-full bg-border overflow-hidden">
        <div
          className={cn('h-full rounded-full transition-all duration-500', barColor)}
          style={{ width: `${score}%` }}
        />
      </div>
      <span className="font-mono text-xs tabular-nums text-muted-foreground w-8 text-right shrink-0">
        {score.toFixed(0)}
      </span>
    </div>
  )
}

// ─── Trend Row ──────────────────────────────────────────────────────────────

function TrendRow({ trend }: { trend: Trend }) {
  const config = TRACTION_CONFIG[trend.tractionLevel]
  const Icon = config.icon
  const date = new Date(trend.lastUpdatedAt)
  const dateStr = date.toLocaleDateString('es-AR', { day: '2-digit', month: 'short', year: '2-digit' })
  const categoryLabel = CATEGORY_LABELS[trend.category] ?? trend.category

  return (
    <tr className="group border-b border-border/50 hover:bg-muted/30 transition-colors">
      {/* Name + summary */}
      <td className="py-3.5 px-4">
        <div className="flex items-center gap-2">
          <p className="font-mono text-sm font-medium text-foreground leading-tight">
            {trend.name}
          </p>
          {trend.gitHubUrl && (
            <a
              href={trend.gitHubUrl}
              target="_blank"
              rel="noopener noreferrer"
              className="opacity-0 group-hover:opacity-100 transition-opacity text-muted-foreground hover:text-foreground"
            >
              <ExternalLink className="h-3 w-3" />
            </a>
          )}
        </div>
        {trend.summary && (
          <p className="mt-0.5 text-xs text-muted-foreground line-clamp-1 max-w-xs">
            {trend.summary}
          </p>
        )}
        {trend.starCount && trend.starCount > 0 && (
          <div className="mt-0.5 flex items-center gap-1 text-xs text-amber-400/80">
            <Star className="h-2.5 w-2.5 fill-current" />
            <span className="font-mono tabular-nums">
              {trend.starCount >= 1000
                ? `${(trend.starCount / 1000).toFixed(1)}k`
                : trend.starCount}
            </span>
          </div>
        )}
      </td>

      {/* Category */}
      <td className="py-3.5 px-4">
        <span className="font-mono text-xs text-muted-foreground">
          {categoryLabel}
        </span>
      </td>

      {/* Traction */}
      <td className="py-3.5 px-4">
        <Badge
          variant="outline"
          className={cn('gap-1 text-xs font-mono', config.className)}
        >
          <Icon className="h-3 w-3" />
          {config.label}
        </Badge>
      </td>

      {/* Score bar */}
      <td className="py-3.5 px-4">
        <ScoreBar score={trend.relevanceScore} tractionLevel={trend.tractionLevel} />
      </td>

      {/* Last updated */}
      <td className="py-3.5 px-4 text-right">
        <span className="font-mono text-xs text-muted-foreground">{dateStr}</span>
      </td>
    </tr>
  )
}

// ─── Skeleton rows ──────────────────────────────────────────────────────────

function SkeletonRow() {
  return (
    <tr className="border-b border-border/50">
      <td className="py-3.5 px-4"><Skeleton className="h-4 w-48 mb-1.5" /><Skeleton className="h-3 w-64" /></td>
      <td className="py-3.5 px-4"><Skeleton className="h-3 w-20" /></td>
      <td className="py-3.5 px-4"><Skeleton className="h-5 w-24 rounded-full" /></td>
      <td className="py-3.5 px-4"><Skeleton className="h-1.5 w-32 rounded-full" /></td>
      <td className="py-3.5 px-4 text-right"><Skeleton className="h-3 w-16 ml-auto" /></td>
    </tr>
  )
}

// ─── Main Component ──────────────────────────────────────────────────────────

const PAGE_SIZE = 20

export function TrendsTable() {
  const [categoryFilter, setCategoryFilter] = useState<string>('')
  const [tractionFilter, setTractionFilter] = useState<string>('')
  const [isSyncing, setIsSyncing] = useState(false)
  const [page, setPage] = useState(1)

  function resetPage() { setPage(1) }

  const { data, isLoading, refetch } = useTrends({
    category: categoryFilter || undefined,
    traction: tractionFilter || undefined,
    page,
    pageSize: PAGE_SIZE,
  })

  const totalPages = data ? Math.ceil(data.totalCount / PAGE_SIZE) : 1

  const trends: Trend[] = data?.items ?? []

  async function handleSync() {
    setIsSyncing(true)
    try {
      const res = await apiClient.post('/api/v1/admin/trends/sync', {})
      if (res.ok) {
        toast.success('Scan en cola', {
          description: 'Estimado: 5 minutos',
          style: { fontFamily: 'var(--font-fira-code)' },
        })
      } else {
        toast.error('Error al iniciar scan', { description: res.error })
      }
    } catch {
      toast.error('Error al iniciar scan')
    } finally {
      setIsSyncing(false)
    }
  }

  return (
    <div className="space-y-4">
      {/* Filter toolbar */}
      <div className="flex flex-wrap items-center gap-3">
        <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
          <Filter className="h-3.5 w-3.5" />
          <span>Filtrar:</span>
        </div>

        <Select value={categoryFilter} onValueChange={(v) => { setCategoryFilter(v ?? ''); resetPage() }}>
          <SelectTrigger className="h-8 w-[160px] text-xs font-mono">
            <SelectValue placeholder="Categoría" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="">Todas las categorías</SelectItem>
            {TREND_CATEGORIES.map((c) => (
              <SelectItem key={c} value={c} className="font-mono text-xs">
                {CATEGORY_LABELS[c] ?? c}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <Select value={tractionFilter} onValueChange={(v) => { setTractionFilter(v ?? ''); resetPage() }}>
          <SelectTrigger className="h-8 w-[160px] text-xs font-mono">
            <SelectValue placeholder="Tracción" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="">Toda tracción</SelectItem>
            {TRACTION_LEVELS.map((t) => (
              <SelectItem key={t} value={t} className="font-mono text-xs">
                {TRACTION_CONFIG[t].label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        {(categoryFilter || tractionFilter) && (
          <Button
            variant="ghost"
            size="sm"
            className="h-8 text-xs text-muted-foreground"
            onClick={() => { setCategoryFilter(''); setTractionFilter(''); resetPage() }}
          >
            Limpiar
          </Button>
        )}

        <div className="flex-1" />

        <Button
          variant="outline"
          size="sm"
          className="h-8 gap-1.5 text-xs font-mono"
          onClick={handleSync}
          disabled={isSyncing}
        >
          <RefreshCw className={cn('h-3 w-3', isSyncing && 'animate-spin')} />
          {isSyncing ? 'Iniciando...' : 'Forzar Scan'}
        </Button>
      </div>

      {/* Stats row */}
      {!isLoading && data && (
        <div className="flex items-center gap-6 text-xs text-muted-foreground font-mono">
          <span>{data.totalCount} tecnologías monitoreadas</span>
          <span className="text-violet-400">
            {trends.filter((t) => t.tractionLevel === 'Growing').length} creciendo
          </span>
          <span className="text-sky-400">
            {trends.filter((t) => t.tractionLevel === 'Emerging').length} emergentes
          </span>
          <span className="text-red-400">
            {trends.filter((t) => t.tractionLevel === 'Declining').length} bajando
          </span>
        </div>
      )}

      {/* Table */}
      <div className="rounded-xl border border-border overflow-hidden">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-border bg-muted/50">
              <th className="py-2.5 px-4 text-left text-xs font-mono font-medium text-muted-foreground">Tecnología</th>
              <th className="py-2.5 px-4 text-left text-xs font-mono font-medium text-muted-foreground">Categoría</th>
              <th className="py-2.5 px-4 text-left text-xs font-mono font-medium text-muted-foreground">Tracción</th>
              <th className="py-2.5 px-4 text-left text-xs font-mono font-medium text-muted-foreground">Relevancia</th>
              <th className="py-2.5 px-4 text-right text-xs font-mono font-medium text-muted-foreground">Actualizado</th>
            </tr>
          </thead>
          <tbody>
            {isLoading
              ? Array.from({ length: 8 }).map((_, i) => <SkeletonRow key={i} />)
              : trends.length === 0
                ? (
                  <tr>
                    <td colSpan={5} className="py-12 text-center text-sm text-muted-foreground">
                      No hay tecnologías que coincidan con los filtros
                    </td>
                  </tr>
                )
                : trends.map((t) => <TrendRow key={t.id} trend={t} />)}
          </tbody>
        </table>
      </div>

      {/* Pagination footer */}
      {!isLoading && totalPages > 1 && (
        <div className="flex items-center justify-between">
          <p className="text-xs text-muted-foreground font-mono">
            Página {page} de {totalPages} · {data?.totalCount} tecnologías
          </p>
          <div className="flex items-center gap-1">
            <Button
              variant="outline"
              size="sm"
              className="h-7 w-7 p-0"
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              disabled={page === 1}
            >
              <ChevronLeft className="h-3.5 w-3.5" />
            </Button>
            <Button
              variant="outline"
              size="sm"
              className="h-7 w-7 p-0"
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              disabled={page === totalPages}
            >
              <ChevronRight className="h-3.5 w-3.5" />
            </Button>
          </div>
        </div>
      )}
      {!isLoading && (
        <p className="text-xs text-muted-foreground font-mono">
          Actualizado: {data?.items?.[0]
            ? new Date(data.items[0].lastUpdatedAt).toLocaleString('es-AR')
            : '—'}
        </p>
      )}
    </div>
  )
}
