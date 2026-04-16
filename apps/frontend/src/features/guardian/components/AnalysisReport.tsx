'use client'

import { useState, useCallback } from 'react'
import { useAction } from 'next-safe-action/hooks'
import { useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { RefreshCw, Loader2, ShieldAlert, ShieldCheck, Copy, Check } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { cn } from '@/lib/utils'
import {
  useRepositoryAnalysis,
  useFindings,
  REPOSITORY_KEYS,
  type Finding,
} from '../hooks/useRepositories'
import { reanalyzeAction } from '../actions'

const SEVERITY_CONFIG: Record<
  string,
  { label: string; className: string; icon: React.ReactNode }
> = {
  Critical: {
    label: 'Crítico',
    className: 'bg-red-500/20 text-red-400 border-red-500/30',
    icon: <ShieldAlert className="h-3 w-3" />,
  },
  High: {
    label: 'Alto',
    className: 'bg-orange-500/20 text-orange-400 border-orange-500/30',
    icon: <ShieldAlert className="h-3 w-3" />,
  },
  Medium: {
    label: 'Medio',
    className: 'bg-yellow-500/20 text-yellow-400 border-yellow-500/30',
    icon: <ShieldCheck className="h-3 w-3" />,
  },
  Low: {
    label: 'Bajo',
    className: 'bg-sky-500/20 text-sky-400 border-sky-500/30',
    icon: <ShieldCheck className="h-3 w-3" />,
  },
}

interface Props {
  repositoryId: string
  isAnalyzing?: boolean
  onReanalyze?: () => void
}

export function AnalysisReport({ repositoryId, isAnalyzing = false, onReanalyze }: Props) {
  const queryClient = useQueryClient()
  const { data: analysis, isLoading: loadingAnalysis, isFetching } = useRepositoryAnalysis(repositoryId)
  // Always use the most recent COMPLETED report so that a re-analysis in progress
  // doesn't wipe out the previous results while the new run is still running.
  const latestReport = analysis?.reports.find(r => r.status === 'Completed')
  const { data: findingsData, isLoading: loadingFindings } = useFindings(
    repositoryId,
    latestReport?.id ?? null,
  )

  const { execute: reanalyze, isPending: isReanalyzing } = useAction(reanalyzeAction, {
    onSuccess: () => {
      toast.success('Re-análisis solicitado')
      onReanalyze?.()
      queryClient.invalidateQueries({ queryKey: REPOSITORY_KEYS.analysis(repositoryId) })
    },
    onError: ({ error }) => toast.error(error.serverError ?? 'Error al re-analizar'),
  })

  if (loadingAnalysis) {
    return (
      <div className="space-y-4">
        <div className="grid grid-cols-4 gap-4">
          {Array.from({ length: 4 }).map((_, i) => (
            <Skeleton key={i} className="h-28 rounded-xl" />
          ))}
        </div>
        <Skeleton className="h-48 rounded-lg" />
      </div>
    )
  }

  // While a re-analysis is running, show previous results with an overlay indicator.
  // When there are no results at all yet (first analysis in progress), show a waiting state.
  if (!analysis || !latestReport) {
    if (isAnalyzing || isFetching) {
      return (
        <div className="flex h-40 items-center justify-center rounded-xl border border-dashed border-border text-sm text-muted-foreground gap-2">
          <Loader2 className="h-4 w-4 animate-spin" />
          Analizando repositorio…
        </div>
      )
    }
    return (
      <div className="flex h-40 items-center justify-center rounded-xl border border-dashed border-border text-sm text-muted-foreground">
        Sin análisis disponible para este repositorio
      </div>
    )
  }

  const { repositoryInfo } = analysis

  return (
    <div className="space-y-5">
      {/* Repo info + actions */}
      <div className="flex items-center justify-between">
        <div>
          <p className="font-mono text-sm text-foreground/90">{repositoryInfo.gitUrl}</p>
          <p className="text-xs text-muted-foreground mt-0.5">
            {repositoryInfo.primaryLanguage} · Analizado{' '}
            {latestReport.executedAt
              ? new Date(latestReport.executedAt).toLocaleDateString('es-AR')
              : '—'}{' '}
            · {latestReport.analysisDurationSeconds}s
          </p>
        </div>
        <Button
          variant="outline"
          size="sm"
          onClick={() => reanalyze({ repositoryId })}
          disabled={isReanalyzing}
        >
          {isReanalyzing ? (
            <Loader2 className="mr-2 h-3.5 w-3.5 animate-spin" />
          ) : (
            <RefreshCw className="mr-2 h-3.5 w-3.5" />
          )}
          Re-analizar
        </Button>
      </div>

      {/* Score gauges */}

      {/* Findings summary */}
      <div className="flex gap-3 flex-wrap">
        {(['Critical', 'High', 'Medium', 'Low'] as const).map((sev) => {
          const count =
            latestReport.findingsCount[sev.toLowerCase() as keyof typeof latestReport.findingsCount]
          const cfg = SEVERITY_CONFIG[sev]
          return (
            <div
              key={sev}
              className={cn(
                'flex items-center gap-1.5 rounded-lg border px-3 py-1.5',
                cfg.className,
              )}
            >
              {cfg.icon}
              <span className="text-xs font-medium">{count} {cfg.label}</span>
            </div>
          )
        })}
      </div>

      {/* Findings table */}
      <FindingsTable findings={findingsData?.items ?? []} loading={loadingFindings} />
    </div>
  )
}

function FindingsTable({ findings, loading }: { findings: Finding[]; loading: boolean }) {
  const [copied, setCopied] = useState(false)

  const copyToClipboard = useCallback(() => {
    if (findings.length === 0) return
    const header = 'Severidad\tCategoría\tHallazgo\tArchivo'
    const rows = findings.map((f) =>
      [
        f.severity,
        f.category,
        [f.title, f.description].filter(Boolean).join(' — '),
        f.filePath ?? '',
      ].join('\t'),
    )
    navigator.clipboard.writeText([header, ...rows].join('\n')).then(() => {
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    })
  }, [findings])

  return (
    <div className="rounded-lg border border-border overflow-hidden">
      <div className="flex items-center justify-between px-4 py-2 border-b border-border bg-muted/30">
        <span className="text-xs font-medium text-muted-foreground">
          {loading ? '…' : `${findings.length} hallazgos`}
        </span>
        <button
          onClick={copyToClipboard}
          disabled={loading || findings.length === 0}
          className="flex items-center gap-1.5 rounded-md px-2.5 py-1 text-xs text-muted-foreground transition-colors hover:bg-muted hover:text-foreground disabled:pointer-events-none disabled:opacity-40"
        >
          {copied
            ? <><Check className="h-3.5 w-3.5 text-emerald-400" /><span className="text-emerald-400">Copiado</span></>
            : <><Copy className="h-3.5 w-3.5" />Copiar tabla</>}
        </button>
      </div>
      <div className="rounded-lg border border-border overflow-hidden">
        <Table>
          <TableHeader>
            <TableRow className="hover:bg-transparent">
              <TableHead>Severidad</TableHead>
              <TableHead>Categoría</TableHead>
              <TableHead className="w-[40%]">Hallazgo</TableHead>
              <TableHead>Archivo</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {loading
              ? Array.from({ length: 4 }).map((_, i) => (
                  <TableRow key={i}>
                    <TableCell><Skeleton className="h-5 w-16" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-20" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-4/5" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-32" /></TableCell>
                  </TableRow>
                ))
              : findings.map((finding: Finding) => {
                  const cfg = SEVERITY_CONFIG[finding.severity]
                  return (
                    <TableRow key={finding.id} className="align-top">
                      <TableCell>
                        <Badge variant="outline" className={cn('text-xs gap-1', cfg.className)}>
                          {cfg.icon}
                          {cfg.label}
                        </Badge>
                      </TableCell>
                      <TableCell>
                        <span className="text-xs text-muted-foreground">{finding.category}</span>
                      </TableCell>
                      <TableCell>
                        <p className="text-sm font-medium">{finding.title}</p>
                        <p className="text-xs text-muted-foreground mt-0.5 line-clamp-2">
                          {finding.description}
                        </p>
                      </TableCell>
                      <TableCell>
                        {finding.filePath && (
                          <code className="text-xs text-muted-foreground font-mono">
                            {finding.filePath}
                          </code>
                        )}
                      </TableCell>
                    </TableRow>
                  )
                })}
          </TableBody>
        </Table>
      </div>
    </div>
  )
}
