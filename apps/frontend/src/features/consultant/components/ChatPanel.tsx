'use client'

import { useEffect, useRef, useState, useCallback, useOptimistic, useTransition } from 'react'
import { useAction } from 'next-safe-action/hooks'
import { useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { Send, Loader2, Bot, User, Sparkles, WifiOff, ChevronDown, GitBranch, ArrowLeft, Check } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/textarea'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Badge } from '@/components/ui/badge'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { cn } from '@/lib/utils'
import { Markdown } from '@/components/ui/markdown'
import {
  useConversation,
  CONVERSATION_KEYS,
  type ConversationMessage,
} from '../hooks/useConversations'
import { sendMessageAction } from '../actions'
import { useHub } from '@/hooks/useHub'
import { getHubConnection, startHub } from '@/lib/signalr'
import { useUiStore } from '@/store/ui-store'
import { useOfflineQueue } from '@/hooks/useOfflineQueue'
import { useRepositories } from '@/features/guardian/hooks/useRepositories'
import type { AgentType, ContextMode } from '@/lib/schemas'

function repoShortName(gitUrl: string) {
  try {
    const url = new URL(gitUrl)
    return url.pathname.replace(/^\//, '').replace(/\.git$/, '')
  } catch {
    return gitUrl
  }
}

const AGENT_LABELS: Record<AgentType, string> = {
  Knowledge: 'Knowledge',
  Consultant: 'Consultant',
  Radar: 'Radar',
}

const CONTEXT_MODE_LABELS: Record<ContextMode, string> = {
  Auto: 'Auto',
  RepoBound: 'Repo-Bound',
  StackBound: 'Stack-Bound',
  Generic: 'Generic',
}

interface Props {
  conversationId: string | null
  onCreateConversation: (agentType: AgentType, activeRepositoryId?: string, repoGitUrl?: string) => void
  isCreating: boolean
}

function MessageBubble({ message }: { message: ConversationMessage }) {
  const isUser = message.role === 'User'
  return (
    <div className={cn('flex gap-3', isUser && 'flex-row-reverse')}>
      {/* Avatar */}
      <div
        className={cn(
          'flex h-7 w-7 shrink-0 items-center justify-center rounded-full border',
          isUser
            ? 'border-primary/40 bg-primary/20 text-primary'
            : 'border-border bg-muted text-muted-foreground',
        )}
      >
        {isUser ? <User className="h-3.5 w-3.5" /> : <Bot className="h-3.5 w-3.5" />}
      </div>

      {/* Content */}
      <div
        className={cn(
          'max-w-[80%] rounded-xl px-4 py-2.5 text-sm leading-relaxed',
          isUser
            ? 'rounded-tr-sm bg-primary text-primary-foreground'
            : 'rounded-tl-sm border border-border bg-card',
        )}
      >
        {isUser ? (
          <p className="whitespace-pre-wrap">{message.content}</p>
        ) : (
          <Markdown content={message.content} />
        )}
        <p
          className={cn(
            'mt-1 text-xs',
            isUser ? 'text-primary-foreground/60' : 'text-muted-foreground',
          )}
        >
          {new Date(message.createdAt).toLocaleTimeString('es-AR', {
            hour: '2-digit',
            minute: '2-digit',
          })}
        </p>
      </div>
    </div>
  )
}

function TypingIndicator() {
  return (
    <div className="flex gap-3">
      <div className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full border border-border bg-muted text-muted-foreground">
        <Bot className="h-3.5 w-3.5" />
      </div>
      <div className="rounded-xl rounded-tl-sm border border-border bg-card px-4 py-3">
        <div className="flex gap-1.5 items-center">
          <span className="h-1.5 w-1.5 rounded-full bg-muted-foreground/60 animate-bounce [animation-delay:0ms]" />
          <span className="h-1.5 w-1.5 rounded-full bg-muted-foreground/60 animate-bounce [animation-delay:150ms]" />
          <span className="h-1.5 w-1.5 rounded-full bg-muted-foreground/60 animate-bounce [animation-delay:300ms]" />
        </div>
      </div>
    </div>
  )
}

export function ChatPanel({ conversationId, onCreateConversation, isCreating }: Props) {
  const [input, setInput] = useState('')
  const [contextMode, setContextMode] = useState<ContextMode>('Auto')
  const [pendingAgentType, setPendingAgentType] = useState<AgentType>('Knowledge')
  const [pendingRepoId, setPendingRepoId] = useState<string | null>(null)

  const { data: reposData } = useRepositories()

  const { data: conversation, isLoading } = useConversation(conversationId)
  const serverMessages = conversation?.recentMessages ?? []

  // agentType comes from the conversation — chosen at creation, immutable afterwards.
  const agentType = (conversation?.agentType ?? 'Knowledge') as AgentType

  // Sync contextMode with the persisted value on the conversation whenever it loads or changes.
  useEffect(() => {
    if (conversation?.mode) {
      setContextMode(conversation.mode as ContextMode)
    }
  }, [conversation?.mode])

  // Streaming state — one in-flight AI message at a time
  const [streamingContent, setStreamingContent] = useState<string | null>(null)
  // True from the moment ReceiveComplete fires until the transition ends — prevents
  // the TypingIndicator from re-appearing during the refetch gap.
  const streamDoneRef = useRef(false)
  // Buffer ref: accumulate chunks without triggering a re-render per token
  const streamBufferRef = useRef('')
  const flushTimerRef = useRef<ReturnType<typeof setInterval> | null>(null)
  // Resolver that signals the transition to end when SignalR streaming is done
  const streamCompleteRef = useRef<(() => void) | null>(null)
  const bottomRef = useRef<HTMLDivElement>(null)
  const textareaRef = useRef<HTMLTextAreaElement>(null)
  const queryClient = useQueryClient()

  const hubState = useUiStore((s) => s.hubStatus['conversation']?.state ?? 'Unknown')
  const enqueueOfflineAction = useUiStore((s) => s.enqueueOfflineAction)
  const isOffline = hubState !== 'Connected'

  // React 19 useOptimistic: shows user message instantly, stays until transition ends.
  // The transition is held open via streamCompleteRef until SignalR ReceiveComplete fires,
  // so the message is visible for the full HTTP + streaming cycle.
  const [optimisticMessages, addOptimistic] = useOptimistic(
    serverMessages,
    (state: ConversationMessage[], newMsg: ConversationMessage) => [...state, newMsg],
  )
  const [isPending, startTransition] = useTransition()

  // ── SignalR: join/leave hub group when conversation changes ───────────────
  // Also re-join on reconnect — the group membership is lost when the connection drops.
  useEffect(() => {
    if (!conversationId) return
    const connection = getHubConnection('conversation')
    let active = true

    async function joinGroup() {
      if (!active) return
      try {
        // Wait for the connection to be ready if it's still starting up
        if (connection.state !== 'Connected') {
          await startHub('conversation')
        }
        if (active) {
          await connection.invoke('JoinConversation', conversationId)
        }
      } catch {
        // Will retry on next reconnect
      }
    }

    joinGroup()
    // Re-join automatically after reconnect (group membership is server-side, lost on drop)
    connection.onreconnected(joinGroup)

    return () => {
      active = false
      connection.invoke('LeaveConversation', conversationId).catch(() => {})
    }
  }, [conversationId])

  // ── SignalR: conversation hub ──────────────────────────────────────────────
  // Backend sends ReceiveToken(token) — single arg, no convId prefix.
  // The hub group is scoped to the conversationId so filtering by convId is unnecessary.
  const handleReceiveChunk = useCallback((token: string) => {
    streamBufferRef.current += token
  }, [])

  const handleReceiveComplete = useCallback(async () => {
    // Stop the flush timer and do a final flush of any buffered content
    if (flushTimerRef.current) clearInterval(flushTimerRef.current)
    flushTimerRef.current = null
    if (streamBufferRef.current) {
      setStreamingContent((prev) => (prev ?? '') + streamBufferRef.current)
      streamBufferRef.current = ''
    }

    // Mark streaming as done BEFORE the async refetch so the TypingIndicator
    // doesn't reappear during the gap between setStreamingContent(null) and
    // the transition ending.
    streamDoneRef.current = true

    // Refetch the conversation so serverMessages is up to date BEFORE releasing the
    // transition. This way useOptimistic hands off to real data, not an empty array.
    if (conversationId) {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: CONVERSATION_KEYS.detail(conversationId) }),
        queryClient.invalidateQueries({ queryKey: CONVERSATION_KEYS.list() }),
      ])
    }

    setStreamingContent(null)
    // Release the transition — useOptimistic now merges with fresh serverMessages
    streamCompleteRef.current?.()
    streamCompleteRef.current = null
    streamDoneRef.current = false
  }, [conversationId, queryClient])

  const handleReceiveError = useCallback((message: string) => {
    if (flushTimerRef.current) clearInterval(flushTimerRef.current)
    flushTimerRef.current = null
    setStreamingContent(null)
    streamBufferRef.current = ''
    streamDoneRef.current = false
    toast.error(message ?? 'Error en la respuesta del agente')
    // Release the transition so useOptimistic auto-reverts the optimistic message
    streamCompleteRef.current?.()
    streamCompleteRef.current = null
  }, [])

  useHub('conversation', {
    handlers: {
      ReceiveToken: handleReceiveChunk as (...args: unknown[]) => void,
      ReceiveComplete: handleReceiveComplete as (...args: unknown[]) => void,
      ReceiveError: handleReceiveError as (...args: unknown[]) => void,
    },
  })

  // Flush buffer to React state at ~16fps to batch micro-updates
  useEffect(() => {
    if (streamingContent === null) return
    flushTimerRef.current = setInterval(() => {
      if (streamBufferRef.current) {
        setStreamingContent((prev) => (prev ?? '') + streamBufferRef.current)
        streamBufferRef.current = ''
      }
    }, 60)
    return () => {
      if (flushTimerRef.current) clearInterval(flushTimerRef.current)
    }
  }, [streamingContent !== null]) // eslint-disable-line react-hooks/exhaustive-deps

  const { executeAsync } = useAction(sendMessageAction, {
    onSuccess: () => {
      // HTTP 200: message enqueued on server. Start streaming animation.
      // Don't invalidate yet — wait for SignalR ReceiveComplete.
      streamDoneRef.current = false
      streamBufferRef.current = ''
      setStreamingContent('')
    },
  })

  // Flush queued messages when the hub reconnects
  useOfflineQueue({ execute: (payload) => executeAsync(payload) })

  // Auto-scroll on new content
  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [optimisticMessages.length, isPending, streamingContent])

  function send() {
    const trimmed = input.trim()
    if (!trimmed || !conversationId || isPending) return

    setInput('')

    // If the hub is disconnected, save to offline queue and notify user
    if (isOffline) {
      enqueueOfflineAction({
        type: 'send_message',
        payload: { conversationId, content: trimmed, contextMode },
      })
      toast.info('Sin conexión — mensaje guardado en cola.', {
        icon: <WifiOff className="h-4 w-4" />,
        style: { fontFamily: 'var(--font-fira-code)' },
      })
      return
    }

    const optimistic: ConversationMessage = {
      id: `opt-${Date.now()}`,
      role: 'User',
      content: trimmed,
      createdAt: new Date().toISOString(),
    }

    // Create a promise that resolves when SignalR ReceiveComplete (or ReceiveError) fires.
    // This keeps the transition alive — and thus useOptimistic visible — for the full
    // HTTP request + streaming cycle.
    const streamDonePromise = new Promise<void>((resolve) => {
      streamCompleteRef.current = resolve
    })

    startTransition(async () => {
      addOptimistic(optimistic)
      const result = await executeAsync({
        conversationId,
        content: trimmed,
        contextMode,
        // Pass the repo so the backend routes to the right RepositoryContextPlugin context
        activeRepositoryId: conversation?.activeRepositoryId ?? undefined,
      })
      if (result?.serverError) {
        toast.error(result.serverError, { style: { fontFamily: 'var(--font-fira-code)' } })
        streamCompleteRef.current?.()
        streamCompleteRef.current = null
        return
      }
      // Hold the transition open until SignalR finishes streaming.
      // Safety timeout: if ReceiveComplete never fires (hub down, backend error), release after 30s
      // to avoid the typing indicator getting stuck permanently.
      const timeout = setTimeout(() => {
        streamCompleteRef.current?.()
        streamCompleteRef.current = null
        setStreamingContent(null)
      }, 30_000)
      await streamDonePromise
      clearTimeout(timeout)
    })
  }

  function handleKeyDown(e: React.KeyboardEvent<HTMLTextAreaElement>) {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      send()
    }
  }

  if (!conversationId) {
    const completedRepos = reposData?.items.filter((r) => r.processingStatus === 'Completed') ?? []
    const isConsultant = pendingAgentType === 'Consultant'

    return (
      <div className="flex h-full flex-col items-center justify-center gap-6 text-center px-8">
        <div className="flex h-14 w-14 items-center justify-center rounded-2xl border border-primary/30 bg-primary/10">
          <Sparkles className="h-6 w-6 text-primary" />
        </div>
        <div>
          <p className="font-medium text-base">Nueva consulta</p>
          <p className="mt-1 text-sm text-muted-foreground">
            {isConsultant
              ? 'Seleccioná el repositorio a analizar'
              : 'Elegí el tipo de agente para esta conversación'}
          </p>
        </div>

        {/* Agent type selector — shown unless Consultant is already chosen */}
        {!isConsultant && (
          <div className="flex gap-3">
            {(Object.keys(AGENT_LABELS) as AgentType[]).map((a) => (
              <button
                key={a}
                onClick={() => { setPendingAgentType(a); setPendingRepoId(null) }}
                className={cn(
                  'flex flex-col items-center gap-2 rounded-xl border px-6 py-4 text-sm transition-colors',
                  pendingAgentType === a
                    ? 'border-primary bg-primary/10 text-primary font-medium'
                    : 'border-border bg-card text-muted-foreground hover:border-primary/50 hover:text-foreground',
                )}
              >
                <Bot className="h-5 w-5" />
                {AGENT_LABELS[a]}
              </button>
            ))}
          </div>
        )}

        {/* Consultant → repo picker */}
        {isConsultant && (
          <div className="w-full max-w-xs flex flex-col gap-2">
            <button
              onClick={() => { setPendingAgentType('Knowledge'); setPendingRepoId(null) }}
              className="flex items-center gap-1.5 text-xs text-muted-foreground hover:text-foreground self-start"
            >
              <ArrowLeft className="h-3.5 w-3.5" />
              Cambiar tipo
            </button>
            {completedRepos.length === 0 ? (
              <p className="text-xs text-muted-foreground text-center py-4">
                No hay repositorios analizados todavía.<br />Subí uno en Guardian primero.
              </p>
            ) : (
              <div className="flex flex-col gap-1 max-h-48 overflow-y-auto border rounded-lg p-1">
                {completedRepos.map((repo) => (
                  <button
                    key={repo.id}
                    onClick={() => setPendingRepoId(repo.id)}
                    className={cn(
                      'flex items-center gap-2 rounded-md px-2.5 py-2 text-left text-xs transition-colors w-full',
                      pendingRepoId === repo.id
                        ? 'bg-primary/15 border border-primary/30 text-foreground'
                        : 'hover:bg-muted/60 text-foreground/80',
                    )}
                  >
                    <GitBranch className="h-3.5 w-3.5 shrink-0 text-muted-foreground" />
                    <span className="flex-1 truncate font-mono">{repoShortName(repo.gitUrl)}</span>
                    {pendingRepoId === repo.id && <Check className="h-3 w-3 shrink-0 text-primary" />}
                  </button>
                ))}
              </div>
            )}
          </div>
        )}

        <Button
          onClick={() => {
            const repo = completedRepos.find((r) => r.id === pendingRepoId)
            onCreateConversation(pendingAgentType, pendingRepoId ?? undefined, repo?.gitUrl)
          }}
          disabled={isCreating || (isConsultant && !pendingRepoId)}
          className="gap-2"
        >
          {isCreating ? <Loader2 className="h-4 w-4 animate-spin" /> : <Sparkles className="h-4 w-4" />}
          Empezar
        </Button>
      </div>
    )
  }

  return (
    <div className="flex h-full flex-col">
      {/* Conversation header */}
      {conversation && (
        <div className="border-b border-border px-4 py-2.5 shrink-0">
          <div className="flex items-center gap-2">
            <Bot className="h-4 w-4 text-primary shrink-0" />
            <p className="flex-1 truncate text-sm font-medium">{conversation.title}</p>
            <Badge variant="outline" className="text-xs shrink-0">
              {conversation.agentType}
            </Badge>
            {isOffline && (
              <Badge variant="destructive" className="gap-1 text-xs font-mono shrink-0">
                <WifiOff className="h-3 w-3" />
                Offline
              </Badge>
            )}
          </div>
          {conversation.activeRepositoryUrl && (
            <div className="flex items-center gap-1.5 mt-1 ml-6">
              <GitBranch className="h-3 w-3 text-primary/70 shrink-0" />
              <span
                className="text-xs font-mono text-primary/80 truncate"
                title={`${conversation.activeRepositoryUrl} · ${conversation.activeRepositoryBranch ?? 'main'}`}
              >
                {repoShortName(conversation.activeRepositoryUrl)}
              </span>
              <span className="text-xs text-muted-foreground">·</span>
              <span className="text-xs font-mono text-muted-foreground shrink-0">
                {conversation.activeRepositoryBranch ?? 'main'}
              </span>
            </div>
          )}
        </div>
      )}

      {/* Messages */}
      <ScrollArea className="flex-1 min-h-0">
        <div className="flex flex-col gap-4 p-4">
          {isLoading ? (
            <>
              {Array.from({ length: 3 }).map((_, i) => (
                <div key={i} className={cn('flex gap-3', i % 2 === 1 && 'flex-row-reverse')}>
                  <div className="h-7 w-7 rounded-full bg-muted animate-pulse shrink-0" />
                  <div
                    className="h-16 rounded-xl bg-muted animate-pulse"
                    style={{ width: `${50 + Math.random() * 30}%` }}
                  />
                </div>
              ))}
            </>
          ) : (
            optimisticMessages.map((msg) => <MessageBubble key={msg.id} message={msg} />)
          )}
          {/* Live streaming bubble — renders while AI is typing */}
          {streamingContent !== null && (
            <div className="flex gap-3">
              <div className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full border border-border bg-muted text-muted-foreground">
                <Bot className="h-3.5 w-3.5" />
              </div>
              <div className="max-w-[80%] rounded-xl rounded-tl-sm border border-primary/30 bg-card px-4 py-2.5 text-sm leading-relaxed">
                {streamingContent ? (
                  <div className="relative">
                    <Markdown content={streamingContent} />
                    <span className="ml-0.5 inline-block h-3.5 w-0.5 bg-primary animate-pulse align-middle" />
                  </div>
                ) : (
                  <TypingIndicator />
                )}
              </div>
            </div>
          )}
          {isPending && streamingContent === null && !streamDoneRef.current && <TypingIndicator />}
          <div ref={bottomRef} />
        </div>
      </ScrollArea>

      {/* Input area */}
      <div className="border-t border-border p-4 shrink-0">
        {/* Agent badge (read-only) + context mode selector */}
        <div className="flex gap-2 mb-2">
          <Badge variant="outline" className="h-7 gap-1 px-2 text-xs font-mono font-normal">
            <Bot className="h-3 w-3" />
            {AGENT_LABELS[agentType]}
          </Badge>

          {agentType === 'Consultant' && (
            <DropdownMenu>
              <DropdownMenuTrigger
                disabled={isPending}
                className="inline-flex h-7 items-center gap-1 rounded-md border border-input bg-background px-2 font-mono text-xs shadow-xs hover:bg-accent hover:text-accent-foreground disabled:opacity-50"
              >
                {CONTEXT_MODE_LABELS[contextMode]}
                <ChevronDown className="h-3 w-3 text-muted-foreground" />
              </DropdownMenuTrigger>
              <DropdownMenuContent align="start">
                <div className="px-2 py-1.5 text-xs font-semibold text-muted-foreground">Modo de contexto</div>
                <DropdownMenuSeparator />
                {(Object.keys(CONTEXT_MODE_LABELS) as ContextMode[]).map((m) => (
                  <DropdownMenuItem
                    key={m}
                    className={cn('text-xs', contextMode === m && 'font-medium text-primary')}
                    onClick={() => setContextMode(m)}
                  >
                    {CONTEXT_MODE_LABELS[m]}
                  </DropdownMenuItem>
                ))}
              </DropdownMenuContent>
            </DropdownMenu>
          )}
        </div>

        <div className="flex gap-2 items-end">
          <Textarea
            ref={textareaRef}
            value={input}
            onChange={(e) => setInput(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder="Escribí tu consulta... (Enter para enviar, Shift+Enter nueva línea)"
            rows={2}
            className="resize-none min-h-[64px] max-h-32"
            disabled={isPending}
          />
          <Button
            size="icon"
            onClick={send}
            disabled={!input.trim() || isPending}
            className="h-10 w-10 shrink-0"
          >
            {isPending ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              <Send className="h-4 w-4" />
            )}
          </Button>
        </div>
        <p className="mt-1.5 text-xs text-muted-foreground">
          Powered by Semantic Kernel · RAG over your knowledge base
        </p>
      </div>
    </div>
  )
}
