'use client'

import { useRef, useState } from 'react'
import { useAction } from 'next-safe-action/hooks'
import { useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { MessageSquare, Plus, Archive, Clock, Layers, Loader2, ChevronDown, Bot, Brain, Trash2 } from 'lucide-react'
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
import { archiveConversationAction, deleteConversationAction } from '../actions'
import type { AgentType } from '@/lib/schemas'

const STATUS_ICON: Record<string, React.ReactNode> = {
  Active: <Clock className="h-3 w-3 text-primary" />,
  Compacted: <Layers className="h-3 w-3 text-amber-400" />,
  Archived: <Archive className="h-3 w-3 text-muted-foreground" />,
}

interface Props {
  activeId: string | null
  onSelect: (id: string) => void
  onCreateConversation: (agentType: AgentType) => void
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

export function ConversationList({ activeId, onSelect, onCreateConversation, isCreating, onDeselect }: Props) {
  const queryClient = useQueryClient()
  const { data, isLoading } = useConversations()
  const [archivingId, setArchivingId] = useState<string | null>(null)
  const [deletingId, setDeletingId] = useState<string | null>(null)
  const deletingIdRef = useRef<string | null>(null)

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

  const active = data?.items.filter((c) => c.status === 'Active') ?? []
  const others = data?.items.filter((c) => c.status !== 'Active') ?? []

  return (
    <div className="flex h-full flex-col">
      {/* New conversation */}
      <div className="p-3 shrink-0">
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
            <DropdownMenuItem className="gap-2 text-sm" onClick={() => onCreateConversation('Consultant')}>
              <Brain className="h-4 w-4 text-muted-foreground" />
              <div>
                <p className="font-medium">Consultant</p>
                <p className="text-xs text-muted-foreground">Consultoría de arquitectura</p>
              </div>
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
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
