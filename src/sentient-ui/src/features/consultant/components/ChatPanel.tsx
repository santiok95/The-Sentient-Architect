'use client'

import { useEffect, useRef, useState, useCallback, useOptimistic, useTransition } from 'react'
import { useAction } from 'next-safe-action/hooks'
import { useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { Send, Loader2, Bot, User, Sparkles, WifiOff } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/textarea'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Badge } from '@/components/ui/badge'
import { cn } from '@/lib/utils'
import {
  useConversation,
  CONVERSATION_KEYS,
  type ConversationMessage,
} from '../hooks/useConversations'
import { sendMessageAction } from '../actions'
import { useHub } from '@/hooks/useHub'
import { useUiStore } from '@/store/ui-store'
import { useOfflineQueue } from '@/hooks/useOfflineQueue'

interface Props {
  conversationId: string | null
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
        <p className="whitespace-pre-wrap">{message.content}</p>
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

export function ChatPanel({ conversationId }: Props) {
  const [input, setInput] = useState('')
  // Streaming state — one in-flight AI message at a time
  const [streamingContent, setStreamingContent] = useState<string | null>(null)
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

  const { data: conversation, isLoading } = useConversation(conversationId)
  const serverMessages = conversation?.recentMessages ?? []

  // React 19 useOptimistic: shows user message instantly, stays until transition ends.
  // The transition is held open via streamCompleteRef until SignalR ReceiveComplete fires,
  // so the message is visible for the full HTTP + streaming cycle.
  const [optimisticMessages, addOptimistic] = useOptimistic(
    serverMessages,
    (state: ConversationMessage[], newMsg: ConversationMessage) => [...state, newMsg],
  )
  const [isPending, startTransition] = useTransition()

  // ── SignalR: conversation hub ──────────────────────────────────────────────
  const handleReceiveChunk = useCallback((convId: string, token: string) => {
    if (convId !== conversationId) return
    streamBufferRef.current += token
  }, [conversationId])

  const handleReceiveComplete = useCallback((convId: string) => {
    if (convId !== conversationId) return
    // Flush remaining buffer and close stream
    if (flushTimerRef.current) clearInterval(flushTimerRef.current)
    flushTimerRef.current = null
    setStreamingContent(null)
    streamBufferRef.current = ''
    queryClient.invalidateQueries({ queryKey: CONVERSATION_KEYS.detail(convId) })
    queryClient.invalidateQueries({ queryKey: CONVERSATION_KEYS.list() })
    // Release the transition: useOptimistic merges with the incoming server state
    streamCompleteRef.current?.()
    streamCompleteRef.current = null
  }, [conversationId, queryClient])

  const handleReceiveError = useCallback((convId: string, message: string) => {
    if (convId !== conversationId) return
    if (flushTimerRef.current) clearInterval(flushTimerRef.current)
    flushTimerRef.current = null
    setStreamingContent(null)
    streamBufferRef.current = ''
    toast.error(message ?? 'Error en la respuesta del agente')
    // Release the transition so useOptimistic auto-reverts the optimistic message
    streamCompleteRef.current?.()
    streamCompleteRef.current = null
  }, [conversationId])

  useHub('conversation', {
    handlers: {
      ReceiveMessageChunk: handleReceiveChunk as (...args: unknown[]) => void,
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
        payload: { conversationId, content: trimmed, mode: 'Auto' },
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
      const result = await executeAsync({ conversationId, content: trimmed, mode: 'Auto' })
      if (result?.serverError) {
        toast.error(result.serverError, { style: { fontFamily: 'var(--font-fira-code)' } })
        streamCompleteRef.current?.()
        streamCompleteRef.current = null
        return
      }
      // Hold the transition open until SignalR finishes streaming
      await streamDonePromise
    })
  }

  function handleKeyDown(e: React.KeyboardEvent<HTMLTextAreaElement>) {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      send()
    }
  }

  if (!conversationId) {
    return (
      <div className="flex h-full flex-col items-center justify-center gap-3 text-center">
        <div className="flex h-14 w-14 items-center justify-center rounded-2xl border border-primary/30 bg-primary/10">
          <Sparkles className="h-6 w-6 text-primary" />
        </div>
        <div>
          <p className="font-medium">Architecture Consultant</p>
          <p className="mt-1 text-sm text-muted-foreground">
            Seleccioná una conversación o creá una nueva
          </p>
        </div>
      </div>
    )
  }

  return (
    <div className="flex h-full flex-col">
      {/* Conversation header */}
      {conversation && (
        <div className="flex items-center gap-2 border-b border-border px-4 py-3 shrink-0">
          <Bot className="h-4 w-4 text-primary" />
          <p className="flex-1 truncate text-sm font-medium">{conversation.objective}</p>
          <Badge variant="outline" className="text-xs">
            {conversation.mode}
          </Badge>
          {isOffline && (
            <Badge variant="destructive" className="gap-1 text-xs font-mono">
              <WifiOff className="h-3 w-3" />
              Offline
            </Badge>
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
                  <p className="whitespace-pre-wrap">{streamingContent}<span className="ml-0.5 inline-block h-3.5 w-0.5 bg-primary animate-pulse" /></p>
                ) : (
                  <TypingIndicator />
                )}
              </div>
            </div>
          )}
          {isPending && streamingContent === null && <TypingIndicator />}
          <div ref={bottomRef} />
        </div>
      </ScrollArea>

      {/* Input area */}
      <div className="border-t border-border p-4 shrink-0">
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
