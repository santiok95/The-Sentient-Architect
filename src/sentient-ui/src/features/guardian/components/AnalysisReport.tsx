'use client'

import { useAction } from 'next-safe-action/hooks'
import { useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { RefreshCw, Loader2, ShieldAlert, ShieldCheck, Wrench, TrendingUp } from 'lucide-react'
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
    label: 'Critical',
    className: 'bg-red-500/20 text-red-400 border-red-500/30',
    icon: <ShieldAlert className="h-3 w-3" />,
  },
  High: {
    label: 'High',
    className: 'bg-orange-500/20 text-orange-400 border-orange-500/30',
    icon: <ShieldAlert className="h-3 w-3" />,
  },
  Medium: {
    label: 'Medium',
    className: 'bg-yellow-500/20 text-yellow-400 border-yellow-500/30',
    icon: <ShieldCheck className="h-3 w-3" />,
  },
  Low: {
    label: 'Low',
    className: 'bg-sky-500/20 text-sky-400 border-sky-500/30',
    icon: <ShieldCheck className="h-3 w-3" />,
  },
}

function ScoreGauge({ label, score, icon }: { label: string; score: number; icon: React.ReactNode }) {
  const color =
    score >= 80 ? 'text-emerald-400' : score >= 60 ? 'text-amber-400' : 'text-red-400'
  const bgColor =
    score >= 80 ? 'bg-emerald-500/20' : score >= 60 ? 'bg-amber-500/20' : 'bg-red-500/20'

  return (
    <div className="flex flex-col items-center gap-2 rounded-xl border border-border bg-card p-4">
      <div className={cn('flex h-10 w-10 items-center justify-center rounded-lg', bgColor)}>
        <span className={cn('text-xl', color)}>{icon}</span>
      </div>
      <div className="text-center">
        <p className={cn('text-2xl font-bold font-mono', color)}>
          {score.toFixed(0)}
        </p>
        <p className="text-xs text-muted-foreground mt-0.5">{label}</p>
      </div>
      {/* Progress bar */}
      <div className="h-1.5 w-full rounded-full bg-muted overflow-hidden">
        <div
          className={cn(
            'h-full rounded-full transition-all',
            score >= 80 ? 'bg-emerald-400' : score >= 60 ? 'bg-amber-400' : 'bg-red-400',
          )}
          style={{ width: `${score}%` }}
        />
      </div>
    </div>
  )
}

interface Props {
  repositoryId: string
}

export function AnalysisReport({ repositoryId }: Props) {
  const queryClient = useQueryClient()
  const { data: analysis, isLoading: loadingAnalysis } = useRepositoryAnalysis(repositoryId)
  const latestReport = analysis?.reports[0]
  const { data: findingsData, isLoading: loadingFindings } = useFindings(
    repositoryId,
    latestReport?.id ?? null,
  )

  const { execute: reanalyze, isPending: isReanalyzing } = useAction(reanalyzeAction, {
    onSuccess: () => {
      toast.success('Re-análisis solicitado')
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

  if (!analysis || !latestReport) {
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
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <ScoreGauge
          label="Overall"
          score={latestReport.overallHealthScore}
          icon={<TrendingUp className="h-5 w-5" />}
        />
        <ScoreGauge
          label="Seguridad"
          score={latestReport.securityScore}
          icon={<ShieldAlert className="h-5 w-5" />}
        />
        <ScoreGauge
          label="Calidad"
          score={latestReport.qualityScore}
          icon={<ShieldCheck className="h-5 w-5" />}
        />
        <ScoreGauge
          label="Mantenibilidad"
          score={latestReport.maintainabilityScore}
          icon={<Wrench className="h-5 w-5" />}
        />
      </div>

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
              <span className="text-xs font-medium">{count} {sev}</span>
            </div>
          )
        })}
      </div>

      {/* Findings table */}
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
            {loadingFindings
              ? Array.from({ length: 4 }).map((_, i) => (
                  <TableRow key={i}>
                    <TableCell><Skeleton className="h-5 w-16" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-20" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-4/5" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-32" /></TableCell>
                  </TableRow>
                ))
              : (findingsData?.items ?? []).map((finding: Finding) => {
                  const cfg = SEVERITY_CONFIG[finding.severity]
                  return (
                    <TableRow key={finding.id} className="align-top">
                      <TableCell>
                        <Badge variant="outline" className={cn('text-xs gap-1', cfg.className)}>
                          {cfg.icon}
                          {finding.severity}
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
