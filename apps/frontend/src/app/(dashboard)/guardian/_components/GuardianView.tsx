'use client'

import { useRef, useState } from 'react'
import { useAction } from 'next-safe-action/hooks'
import { useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { GitFork, Clock, CheckCircle2, Loader2, AlertCircle, Trash2 } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import { ScrollArea } from '@/components/ui/scroll-area'
import { cn } from '@/lib/utils'
import { SubmitRepoForm } from '@/features/guardian/components/SubmitRepoForm'
import { AnalysisReport } from '@/features/guardian/components/AnalysisReport'
import { AnalysisLiveLog } from '@/features/guardian/components/AnalysisLiveLog'
import { useRepositories, REPOSITORY_KEYS, type RepositorySummary } from '@/features/guardian/hooks/useRepositories'
import { deleteRepoAction } from '@/features/guardian/actions'

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
  isDeleting,
  onClick,
  onDelete,
}: {
  repo: RepositorySummary
  isActive: boolean
  isDeleting: boolean
  onClick: () => void
  onDelete: () => void
}) {
  const name = repo.gitUrl.replace('https://github.com/', '')
  const status = STATUS_CONFIG[repo.processingStatus] ?? STATUS_CONFIG.Pending

  return (
    <div
      className={cn(
        'group relative w-full text-left rounded-xl border p-4 transition-all cursor-pointer hover:bg-muted/50',
        isActive
          ? 'border-primary/50 bg-primary/10 ring-1 ring-primary/30'
          : 'border-border bg-card',
      )}
      onClick={onClick}
    >
      <div className="flex items-start gap-3">
        <div className={cn('mt-0.5 flex h-7 w-7 shrink-0 items-center justify-center rounded-lg border', status.className)}>
          {status.icon}
        </div>
        <div className="flex-1 min-w-0 pr-7">
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

      {/* Delete button — appears on hover, same pattern as ConversationItem */}
      <button
        className="absolute right-2 top-2 hidden group-hover:flex items-center justify-center rounded h-6 w-6 hover:bg-destructive/10 transition-colors"
        onClick={(e) => { e.stopPropagation(); onDelete() }}
        disabled={isDeleting}
        title="Eliminar repositorio"
      >
        {isDeleting
          ? <Loader2 className="h-3 w-3 animate-spin" />
          : <Trash2 className="h-3 w-3 text-destructive/70 hover:text-destructive" />}
      </button>
    </div>
  )
}

export function GuardianView() {
  const queryClient = useQueryClient()
  const [activeRepoId, setActiveRepoId] = useState<string | null>(null)
  const [analyzingRepoId, setAnalyzingRepoId] = useState<string | null>(null)
  const [deletingId, setDeletingId] = useState<string | null>(null)
  const deletingIdRef = useRef<string | null>(null)
  const { data, isLoading } = useRepositories()

  const repos = data?.items ?? []
  const isAnalyzing = analyzingRepoId === activeRepoId

  const { execute: deleteRepo } = useAction(deleteRepoAction, {
    onSuccess: () => {
      // If the deleted repo was active, clear the panel
      if (deletingIdRef.current && activeRepoId === deletingIdRef.current)
        setActiveRepoId(null)
      queryClient.invalidateQueries({ queryKey: REPOSITORY_KEYS.list() })
      deletingIdRef.current = null
      setDeletingId(null)
      toast.success('Repositorio eliminado')
    },
    onError: ({ error }) => {
      toast.error(error.serverError ?? 'Error al eliminar el repositorio')
      deletingIdRef.current = null
      setDeletingId(null)
    },
  })

  function handleAnalysisComplete(_reportId: string) {
    setAnalyzingRepoId(null)
    queryClient.invalidateQueries({ queryKey: REPOSITORY_KEYS.list() })
    queryClient.invalidateQueries({ queryKey: REPOSITORY_KEYS.analysis(activeRepoId ?? '') })
    queryClient.invalidateQueries({ queryKey: ['repositories', 'findings'] })
  }

  return (
    <div className="grid grid-cols-1 gap-6 lg:grid-cols-[360px_1fr]">
      <div className="flex flex-col gap-4 min-h-0">
        <SubmitRepoForm
          onSubmitted={(repoId) => {
            setActiveRepoId(repoId)
            setAnalyzingRepoId(repoId)
          }}
        />
        <div className="flex flex-col gap-2 min-h-0">
          <p className="text-xs font-medium text-muted-foreground uppercase tracking-wide px-0.5 shrink-0">
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
                      <p className="mt-2 text-sm text-muted-foreground">Sin repositorios todavia</p>
                    </div>
                  </div>
                )
              : (
                  <ScrollArea className="max-h-[calc(100vh-420px)] pr-1">
                    <div className="space-y-2">
                      {repos.map((repo) => (
                        <RepoCard
                          key={repo.id}
                          repo={repo}
                          isActive={activeRepoId === repo.id}
                          isDeleting={deletingId === repo.id}
                          onClick={() => setActiveRepoId(repo.id)}
                          onDelete={() => {
                            deletingIdRef.current = repo.id
                            setDeletingId(repo.id)
                            deleteRepo({ repositoryId: repo.id })
                          }}
                        />
                      ))}
                    </div>
                  </ScrollArea>
                )}
        </div>
      </div>

      <div className="min-w-0 space-y-4">
        <AnalysisLiveLog
          repositoryId={activeRepoId}
          isAnalyzing={isAnalyzing}
          onComplete={handleAnalysisComplete}
        />
        {activeRepoId ? (
          <AnalysisReport
            repositoryId={activeRepoId}
            isAnalyzing={isAnalyzing}
            onReanalyze={() => setAnalyzingRepoId(activeRepoId)}
          />
        ) : (
          <div className="flex h-64 items-center justify-center rounded-xl border border-dashed border-border">
            <div className="text-center">
              <GitFork className="mx-auto h-10 w-10 text-muted-foreground/30" />
              <p className="mt-3 text-sm text-muted-foreground">
                Selecciona un repositorio para ver el analisis
              </p>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
