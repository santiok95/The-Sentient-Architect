'use client'

import { useEffect, useRef, useState } from 'react'
import { useAction } from 'next-safe-action/hooks'
import { useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { Send, Loader2, Bot, User, Sparkles } from 'lucide-react'
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
  const [optimisticMessages, setOptimisticMessages] = useState<ConversationMessage[]>([])
  const bottomRef = useRef<HTMLDivElement>(null)
  const textareaRef = useRef<HTMLTextAreaElement>(null)
  const queryClient = useQueryClient()

  const { data: conversation, isLoading } = useConversation(conversationId)
  const messages = [...(conversation?.recentMessages ?? []), ...optimisticMessages]

  const { execute, isPending } = useAction(sendMessageAction, {
    onSuccess: ({ data }) => {
      setOptimisticMessages([])
      queryClient.invalidateQueries({
        queryKey: CONVERSATION_KEYS.detail(conversationId ?? ''),
      })
      queryClient.invalidateQueries({ queryKey: CONVERSATION_KEYS.list() })
    },
    onError: ({ error }) => {
      setOptimisticMessages([])
      toast.error(error.serverError ?? 'Error al enviar el mensaje')
    },
  })

  // Auto-scroll
  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages.length, isPending])

  function send() {
    const trimmed = input.trim()
    if (!trimmed || !conversationId || isPending) return

    const optimistic: ConversationMessage = {
      id: `opt-${Date.now()}`,
      role: 'User',
      content: trimmed,
      createdAt: new Date().toISOString(),
    }
    setOptimisticMessages([optimistic])
    setInput('')
    execute({ conversationId, content: trimmed, mode: 'Auto' })
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
            messages.map((msg) => <MessageBubble key={msg.id} message={msg} />)
          )}
          {isPending && <TypingIndicator />}
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
