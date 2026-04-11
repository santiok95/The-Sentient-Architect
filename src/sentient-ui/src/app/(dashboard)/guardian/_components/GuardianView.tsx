'use client'

import { useState } from 'react'
import { GitFork, Clock, CheckCircle2, Loader2, AlertCircle } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'
import { SubmitRepoForm } from '@/features/guardian/components/SubmitRepoForm'
import { AnalysisReport } from '@/features/guardian/components/AnalysisReport'
import { AnalysisLiveLog } from '@/features/guardian/components/AnalysisLiveLog'
import { useRepositories, type RepositorySummary } from '@/features/guardian/hooks/useRepositories'

const STATUS_CONFIG: Record<string, { icon: React.ReactNode; className: string }> = {
  Completed: {
    icon: <CheckCircle2 className="h-3.5 w-3.5 text-emerald-400" />,
    className: 'border-emerald-500/30 bg-emerald-500/10',
  },
  Processing: {
    icon: <Loader2 className="h-3.5 w-3.5 text-amber-400 animate-spin" />,
    className: 'border-amber-500/30 bg-amber-500/10',
  },
  Pending: {
    icon: <Clock className="h-3.5 w-3.5 text-sky-400" />,
    className: 'border-sky-500/30 bg-sky-500/10',
  },
  Failed: {
    icon: <AlertCircle className="h-3.5 w-3.5 text-red-400" />,
    className: 'border-red-500/30 bg-red-500/10',
  },
}

function RepoCard({
  repo,
  isActive,
  onClick,
}: {
  repo: RepositorySummary
  isActive: boolean
  onClick: () => void
}) {
  const name = repo.gitUrl.replace('https://github.com/', '')
  const status = STATUS_CONFIG[repo.processingStatus] ?? STATUS_CONFIG.Pending

  return (
    <button
      onClick={onClick}
      className={cn(
        'w-full text-left rounded-xl border p-4 transition-all hover:bg-muted/50',
        isActive
          ? 'border-primary/50 bg-primary/10 ring-1 ring-primary/30'
          : 'border-border bg-card',
      )}
    >
      <div className="flex items-start gap-3">
        <div className={cn('mt-0.5 flex h-7 w-7 shrink-0 items-center justify-center rounded-lg border', status.className)}>
          {status.icon}
        </div>
        <div className="flex-1 min-w-0">
          <p className="truncate text-sm font-mono font-medium">{name}</p>
          <div className="flex items-center gap-2 mt-1 flex-wrap">
            {repo.primaryLanguage && (
              <Badge variant="outline" className="text-xs h-4 px-1.5">
                {repo.primaryLanguage}
              </Badge>
            )}
            <Badge
              variant="outline"
              className={cn(
                'text-xs h-4 px-1.5',
                repo.trustLevel === 'Internal'
                  ? 'text-emerald-400 border-emerald-500/30'
                  : 'text-amber-400 border-amber-500/30',
              )}
            >
              {repo.trustLevel}
            </Badge>
            <span className="text-xs text-muted-foreground">
              {repo.processingStatus}
            </span>
          </div>
        </div>
      </div>
    </button>
  )
}

export function GuardianView() {
  const [activeRepoId, setActiveRepoId] = useState<string | null>(null)
  const { data, isLoading } = useRepositories()

  const repos = data?.items ?? []

  return (
    <div className="grid grid-cols-1 gap-6 lg:grid-cols-[360px_1fr]">
      {/* Left column */}
      <div className="space-y-4">
        {/* Submit form */}
        <SubmitRepoForm />

        {/* Repo list */}
        <div className="space-y-2">
          <p className="text-xs font-medium text-muted-foreground uppercase tracking-wide px-0.5">
            Repositorios analizados
          </p>
          {isLoading
            ? Array.from({ length: 3 }).map((_, i) => (
                <Skeleton key={i} className="h-20 rounded-xl" />
              ))
            : repos.length === 0
              ? (
                  <div className="flex items-center justify-center rounded-xl border border-dashed border-border py-8">
                    <div className="text-center">
                      <GitFork className="mx-auto h-8 w-8 text-muted-foreground/40" />
                      <p className="mt-2 text-sm text-muted-foreground">Sin repositorios todavía</p>
                    </div>
                  </div>
                )
              : repos.map((repo) => (
                  <RepoCard
                    key={repo.id}
                    repo={repo}
                    isActive={activeRepoId === repo.id}
                    onClick={() => {
                      if (repo.processingStatus === 'Completed') {
                        setActiveRepoId(repo.id)
                      }
                    }}
                  />
                ))}
        </div>
      </div>

      {/* Right column: analysis */}
      <div className="min-w-0 space-y-4">
        <AnalysisLiveLog
          repositoryId={activeRepoId}
          onComplete={() => {}}
        />
        {activeRepoId ? (
          <AnalysisReport repositoryId={activeRepoId} />
        ) : (
          <div className="flex h-64 items-center justify-center rounded-xl border border-dashed border-border">
            <div className="text-center">
              <GitFork className="mx-auto h-10 w-10 text-muted-foreground/30" />
              <p className="mt-3 text-sm text-muted-foreground">
                Seleccioná un repositorio completado para ver el análisis
              </p>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
