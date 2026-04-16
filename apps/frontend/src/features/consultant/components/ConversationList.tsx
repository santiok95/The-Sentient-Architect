'use client'

import { useRef, useState } from 'react'
import { useAction } from 'next-safe-action/hooks'
import { useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import {
  MessageSquare, Plus, Archive, Clock, Layers, Loader2,
  ChevronDown, Bot, Brain, Trash2, GitBranch, ArrowLeft, Check,
} from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Separator } from '@/components/ui/separator'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { cn } from '@/lib/utils'
import { useConversations, CONVERSATION_KEYS, type ConversationSummary } from '../hooks/useConversations'
import { useRepositories, type RepositorySummary } from '@/features/guardian/hooks/useRepositories'
import { archiveConversationAction, deleteConversationAction } from '../actions'
import type { AgentType } from '@/lib/schemas'

const STATUS_ICON: Record<string, React.ReactNode> = {
  Active: <Clock className="h-3 w-3 text-primary" />,
  Compacted: <Layers className="h-3 w-3 text-amber-400" />,
  Archived: <Archive className="h-3 w-3 text-muted-foreground" />,
}

function repoShortName(gitUrl: string) {
  // https://github.com/owner/repo → owner/repo
  try {
    const url = new URL(gitUrl)
    return url.pathname.replace(/^\//, '').replace(/\.git$/, '')
  } catch {
    return gitUrl
  }
}

interface Props {
  activeId: string | null
  onSelect: (id: string) => void
  onCreateConversation: (agentType: AgentType, activeRepositoryId?: string, repoGitUrl?: string) => void
  isCreating: boolean
  onDeselect: () => void
}

function ConversationItem({
  conv,
  isActive,
  onSelect,
  onArchive,
  isArchiving,
  onDelete,
  isDeleting,
}: {
  conv: ConversationSummary
  isActive: boolean
  onSelect: () => void
  onArchive: () => void
  isArchiving: boolean
  onDelete: () => void
  isDeleting: boolean
}) {
  return (
    <div
      className={cn(
        'group relative flex cursor-pointer items-start gap-2.5 rounded-lg px-3 py-2.5 transition-colors',
        isActive ? 'bg-primary/15 border border-primary/30' : 'hover:bg-muted/60',
      )}
      onClick={onSelect}
    >
      <div className="mt-0.5 shrink-0">{STATUS_ICON[conv.status]}</div>
      <div className="flex-1 min-w-0 pr-14">
        <p className={cn('truncate text-sm', isActive ? 'text-foreground font-medium' : 'text-foreground/80')}>
          {conv.title}
        </p>
        <div className="flex items-center gap-2 mt-0.5">
          <Badge variant="outline" className="text-xs h-4 px-1">
            {conv.agentType}
          </Badge>
          <span className="text-xs text-muted-foreground">
            {conv.messageCount} msg
          </span>
        </div>
      </div>
      <div className="absolute right-2 top-2 hidden group-hover:flex items-center gap-1">
        {conv.status === 'Active' && (
          <button
            className="flex items-center justify-center rounded h-6 w-6 hover:bg-muted transition-colors"
            onClick={(e) => { e.stopPropagation(); onArchive() }}
            disabled={isArchiving || isDeleting}
            title="Archivar"
          >
            {isArchiving ? (
              <Loader2 className="h-3 w-3 animate-spin" />
            ) : (
              <Archive className="h-3 w-3 text-muted-foreground" />
            )}
          </button>
        )}
        <button
          className="flex items-center justify-center rounded h-6 w-6 hover:bg-destructive/10 transition-colors"
          onClick={(e) => { e.stopPropagation(); onDelete() }}
          disabled={isArchiving || isDeleting}
          title="Eliminar"
        >
          {isDeleting ? (
            <Loader2 className="h-3 w-3 animate-spin" />
          ) : (
            <Trash2 className="h-3 w-3 text-destructive/70 hover:text-destructive" />
          )}
        </button>
      </div>
    </div>
  )
}

// Groups repos by URL — one entry per URL, carrying all trust levels available.
// Uses the Internal variant's id if available (more thorough analysis context).
interface DeduplicatedRepo {
  id: string
  gitUrl: string
  trustLevels: string[]
}

function deduplicateRepos(repos: RepositorySummary[]): DeduplicatedRepo[] {
  const completed = repos.filter((r) => r.processingStatus === 'Completed')
  const byUrl = new Map<string, RepositorySummary[]>()
  for (const repo of completed) {
    const existing = byUrl.get(repo.gitUrl) ?? []
    byUrl.set(repo.gitUrl, [...existing, repo])
  }
  return Array.from(byUrl.entries()).map(([gitUrl, variants]) => {
    const internal = variants.find((r) => r.trustLevel === 'Internal')
    const preferred = internal ?? variants[0]
    return {
      id: preferred.id,
      gitUrl,
      trustLevels: variants.map((r) => r.trustLevel),
    }
  })
}

function RepoPicker({
  repos,
  selectedId,
  onSelect,
  onConfirm,
  onBack,
  isCreating,
}: {
  repos: RepositorySummary[]
  selectedId: string | null
  onSelect: (id: string) => void
  onConfirm: () => void
  onBack: () => void
  isCreating: boolean
}) {
  const deduplicated = deduplicateRepos(repos)

  return (
    <div className="flex flex-col gap-2 px-3 py-2">
      <div className="flex items-center gap-2">
        <button
          onClick={onBack}
          className="flex items-center justify-center rounded h-6 w-6 hover:bg-muted transition-colors"
          title="Volver"
        >
          <ArrowLeft className="h-3.5 w-3.5 text-muted-foreground" />
        </button>
        <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">
          Seleccioná un repositorio
        </p>
      </div>

      {deduplicated.length === 0 ? (
        <p className="text-xs text-muted-foreground px-1 py-2">
          No hay repositorios analizados todavía. Subí uno en Guardian primero.
        </p>
      ) : (
        <div className="flex flex-col gap-1 max-h-48 overflow-y-auto">
          {deduplicated.map((repo) => (
            <button
              key={repo.id}
              onClick={() => onSelect(repo.id)}
              className={cn(
                'flex items-center gap-2 rounded-md px-2 py-1.5 text-left text-xs transition-colors w-full',
                selectedId === repo.id
                  ? 'bg-primary/15 border border-primary/30 text-foreground'
                  : 'hover:bg-muted/60 text-foreground/80',
              )}
            >
              <GitBranch className="h-3.5 w-3.5 shrink-0 text-muted-foreground" />
              <span className="flex-1 truncate font-mono">{repoShortName(repo.gitUrl)}</span>
              <span className="flex gap-1 shrink-0">
                {repo.trustLevels.map((t) => (
                  <span
                    key={t}
                    className={cn(
                      'rounded px-1 py-0.5 text-[10px] font-medium border',
                      t === 'Internal'
                        ? 'text-emerald-400 border-emerald-500/30 bg-emerald-500/10'
                        : 'text-amber-400 border-amber-500/30 bg-amber-500/10',
                    )}
                  >
                    {t}
                  </span>
                ))}
              </span>
              {selectedId === repo.id && <Check className="h-3 w-3 shrink-0 text-primary" />}
            </button>
          ))}
        </div>
      )}

      <div className="flex gap-2 mt-1">
        <Button
          size="sm"
          variant="outline"
          className="flex-1 h-7 text-xs"
          onClick={onBack}
          disabled={isCreating}
        >
          Cancelar
        </Button>
        <Button
          size="sm"
          className="flex-1 h-7 text-xs"
          onClick={onConfirm}
          disabled={isCreating || !selectedId}
        >
          {isCreating ? <Loader2 className="h-3 w-3 animate-spin" /> : 'Empezar'}
        </Button>
      </div>
    </div>
  )
}

export function ConversationList({ activeId, onSelect, onCreateConversation, isCreating, onDeselect }: Props) {
  const queryClient = useQueryClient()
  const { data, isLoading } = useConversations()
  const { data: reposData } = useRepositories()
  const [archivingId, setArchivingId] = useState<string | null>(null)
  const [deletingId, setDeletingId] = useState<string | null>(null)
  const deletingIdRef = useRef<string | null>(null)

  // Repo picker state
  const [showRepoPicker, setShowRepoPicker] = useState(false)
  const [selectedRepoId, setSelectedRepoId] = useState<string | null>(null)

  const { execute: archiveConv } = useAction(archiveConversationAction, {
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: CONVERSATION_KEYS.all })
      setArchivingId(null)
    },
    onError: ({ error }) => {
      toast.error(error.serverError ?? 'Error al archivar')
      setArchivingId(null)
    },
  })

  const { execute: deleteConv } = useAction(deleteConversationAction, {
    onSuccess: () => {
      if (deletingIdRef.current && activeId === deletingIdRef.current) onDeselect()
      queryClient.invalidateQueries({ queryKey: CONVERSATION_KEYS.all })
      deletingIdRef.current = null
      setDeletingId(null)
    },
    onError: ({ error }) => {
      toast.error(error.serverError ?? 'Error al eliminar')
      deletingIdRef.current = null
      setDeletingId(null)
    },
  })

  function handleConsultantClick() {
    setSelectedRepoId(null)
    setShowRepoPicker(true)
  }

  function handleRepoConfirm() {
    if (!selectedRepoId) return
    const repo = repos.find((r) => r.id === selectedRepoId)
    setShowRepoPicker(false)
    onCreateConversation('Consultant', selectedRepoId, repo?.gitUrl)
  }

  function handleRepoBack() {
    setShowRepoPicker(false)
    setSelectedRepoId(null)
  }

  const active = data?.items.filter((c) => c.status === 'Active') ?? []
  const others = data?.items.filter((c) => c.status !== 'Active') ?? []
  const repos = reposData?.items ?? []

  return (
    <div className="flex h-full flex-col">
      {/* New conversation */}
      <div className="p-3 shrink-0">
        {showRepoPicker ? (
          <RepoPicker
            repos={repos}
            selectedId={selectedRepoId}
            onSelect={setSelectedRepoId}
            onConfirm={handleRepoConfirm}
            onBack={handleRepoBack}
            isCreating={isCreating}
          />
        ) : (
          <DropdownMenu>
            <DropdownMenuTrigger
              disabled={isCreating}
              className="inline-flex w-full items-center justify-between gap-2 rounded-md border border-input bg-background px-3 py-1.5 text-sm shadow-xs hover:bg-accent hover:text-accent-foreground disabled:opacity-50 h-8"
            >
              <span className="flex items-center gap-2">
                {isCreating ? <Loader2 className="h-4 w-4 animate-spin" /> : <Plus className="h-4 w-4" />}
                Nueva consulta
              </span>
              <ChevronDown className="h-3 w-3 text-muted-foreground" />
            </DropdownMenuTrigger>
            <DropdownMenuContent align="start" className="w-48">
              <div className="px-2 py-1.5 text-xs font-semibold text-muted-foreground">Tipo de agente</div>
              <DropdownMenuSeparator />
              <DropdownMenuItem className="gap-2 text-sm" onClick={() => onCreateConversation('Knowledge')}>
                <Bot className="h-4 w-4 text-muted-foreground" />
                <div>
                  <p className="font-medium">Knowledge</p>
                  <p className="text-xs text-muted-foreground">Base de conocimiento</p>
                </div>
              </DropdownMenuItem>
              <DropdownMenuItem className="gap-2 text-sm" onClick={handleConsultantClick}>
                <Brain className="h-4 w-4 text-muted-foreground" />
                <div>
                  <p className="font-medium">Consultant</p>
                  <p className="text-xs text-muted-foreground">Consultoría de arquitectura</p>
                </div>
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        )}
      </div>

      <Separator />

      <ScrollArea className="flex-1 min-h-0">
        <div className="p-2 space-y-1">
          {isLoading && (
            <div className="space-y-2 px-1 py-2">
              {Array.from({ length: 4 }).map((_, i) => (
                <div key={i} className="h-14 rounded-lg bg-muted animate-pulse" />
              ))}
            </div>
          )}

          {/* Active */}
          {active.length > 0 && (
            <>
              <p className="px-2 py-1 text-xs font-medium text-muted-foreground uppercase tracking-wide">
                Activas
              </p>
              {active.map((conv) => (
                <ConversationItem
                  key={conv.id}
                  conv={conv}
                  isActive={activeId === conv.id}
                  onSelect={() => onSelect(conv.id)}
                  onArchive={() => { setArchivingId(conv.id); archiveConv({ id: conv.id }) }}
                  isArchiving={archivingId === conv.id}
                  onDelete={() => { deletingIdRef.current = conv.id; setDeletingId(conv.id); deleteConv({ id: conv.id }) }}
                  isDeleting={deletingId === conv.id}
                />
              ))}
            </>
          )}

          {/* Others */}
          {others.length > 0 && (
            <>
              <p className="mt-2 px-2 py-1 text-xs font-medium text-muted-foreground uppercase tracking-wide">
                Anteriores
              </p>
              {others.map((conv) => (
                <ConversationItem
                  key={conv.id}
                  conv={conv}
                  isActive={activeId === conv.id}
                  onSelect={() => onSelect(conv.id)}
                  onArchive={() => {}}
                  isArchiving={false}
                  onDelete={() => { deletingIdRef.current = conv.id; setDeletingId(conv.id); deleteConv({ id: conv.id }) }}
                  isDeleting={deletingId === conv.id}
                />
              ))}
            </>
          )}

          {!isLoading && !data?.items.length && (
            <div className="flex flex-col items-center gap-2 py-10 text-center">
              <MessageSquare className="h-8 w-8 text-muted-foreground/40" />
              <p className="text-xs text-muted-foreground">Sin conversaciones todavía</p>
            </div>
          )}
        </div>
      </ScrollArea>
    </div>
  )
}
