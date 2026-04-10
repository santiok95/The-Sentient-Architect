'use client'

import { useState } from 'react'
import { useAction } from 'next-safe-action/hooks'
import { useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { MessageSquare, Plus, Archive, Clock, CheckCircle2, Loader2 } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Separator } from '@/components/ui/separator'
import { cn } from '@/lib/utils'
import { useConversations, CONVERSATION_KEYS, type ConversationSummary } from '../hooks/useConversations'
import { createConversationAction, archiveConversationAction } from '../actions'

const STATUS_ICON: Record<string, React.ReactNode> = {
  Active: <Clock className="h-3 w-3 text-primary" />,
  Completed: <CheckCircle2 className="h-3 w-3 text-emerald-400" />,
  Archived: <Archive className="h-3 w-3 text-muted-foreground" />,
}

interface Props {
  activeId: string | null
  onSelect: (id: string) => void
}

function ConversationItem({
  conv,
  isActive,
  onSelect,
  onArchive,
  isArchiving,
}: {
  conv: ConversationSummary
  isActive: boolean
  onSelect: () => void
  onArchive: () => void
  isArchiving: boolean
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
      <div className="flex-1 min-w-0">
        <p className={cn('truncate text-sm', isActive ? 'text-foreground font-medium' : 'text-foreground/80')}>
          {conv.objective}
        </p>
        <div className="flex items-center gap-2 mt-0.5">
          <Badge variant="outline" className="text-xs h-4 px-1">
            {conv.mode}
          </Badge>
          <span className="text-xs text-muted-foreground">
            {conv.messageCount} msg
          </span>
        </div>
      </div>
      {conv.status === 'Active' && (
        <button
          className="absolute right-2 top-2 hidden group-hover:flex items-center justify-center rounded h-6 w-6 hover:bg-muted transition-colors"
          onClick={(e) => {
            e.stopPropagation()
            onArchive()
          }}
          disabled={isArchiving}
          title="Archivar"
        >
          {isArchiving ? (
            <Loader2 className="h-3 w-3 animate-spin" />
          ) : (
            <Archive className="h-3 w-3 text-muted-foreground" />
          )}
        </button>
      )}
    </div>
  )
}

export function ConversationList({ activeId, onSelect }: Props) {
  const queryClient = useQueryClient()
  const { data, isLoading } = useConversations()
  const [archivingId, setArchivingId] = useState<string | null>(null)

  const { execute: createConv, isPending: isCreating } = useAction(createConversationAction, {
    onSuccess: ({ data: newConv }) => {
      queryClient.invalidateQueries({ queryKey: CONVERSATION_KEYS.all })
      if (newConv?.id) onSelect(newConv.id)
    },
    onError: ({ error }) => toast.error(error.serverError ?? 'Error al crear conversación'),
  })

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

  const active = data?.items.filter((c) => c.status === 'Active') ?? []
  const others = data?.items.filter((c) => c.status !== 'Active') ?? []

  return (
    <div className="flex h-full flex-col">
      {/* New conversation */}
      <div className="p-3 shrink-0">
        <Button
          className="w-full justify-start gap-2"
          variant="outline"
          size="sm"
          onClick={() => createConv({ mode: 'Auto' })}
          disabled={isCreating}
        >
          {isCreating ? (
            <Loader2 className="h-4 w-4 animate-spin" />
          ) : (
            <Plus className="h-4 w-4" />
          )}
          Nueva consulta
        </Button>
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
                  onArchive={() => {
                    setArchivingId(conv.id)
                    archiveConv({ id: conv.id })
                  }}
                  isArchiving={archivingId === conv.id}
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
