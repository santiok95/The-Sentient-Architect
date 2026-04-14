'use client'

import { useState } from 'react'
import { useAction } from 'next-safe-action/hooks'
import { useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { ConversationList } from '@/features/consultant/components/ConversationList'
import { ChatPanel } from '@/features/consultant/components/ChatPanel'
import { createConversationAction } from '@/features/consultant/actions'
import { CONVERSATION_KEYS } from '@/features/consultant/hooks/useConversations'
import type { AgentType } from '@/lib/schemas'

function repoShortName(gitUrl: string) {
  try {
    const url = new URL(gitUrl)
    return url.pathname.replace(/^\//, '').replace(/\.git$/, '')
  } catch {
    return gitUrl
  }
}

export function ConsultantView() {
  const [activeConversationId, setActiveConversationId] = useState<string | null>(null)
  const [chatKey, setChatKey] = useState(0)
  const queryClient = useQueryClient()

  function selectConversation(id: string | null) {
    setActiveConversationId(id)
    setChatKey((k) => k + 1)
  }

  const { execute: createConv, isPending: isCreating } = useAction(createConversationAction, {
    onSuccess: ({ data: newConv }) => {
      queryClient.invalidateQueries({ queryKey: CONVERSATION_KEYS.all })
      selectConversation(newConv?.id ?? null)
    },
    onError: ({ error }) => toast.error(error.serverError ?? 'Error al crear conversación'),
  })

  function handleCreateConversation(agentType: AgentType, activeRepositoryId?: string, repoGitUrl?: string) {
    selectConversation(null)
    const title = agentType === 'Consultant' && repoGitUrl
      ? `Consulta — ${repoShortName(repoGitUrl)}`
      : undefined  // backend default
    createConv({ agentType, activeRepositoryId, title })
  }

  return (
    <div className="flex h-full">
      {/* Sidebar: conversation list */}
      <div className="w-72 shrink-0 border-r border-border bg-card/50">
        <ConversationList
          activeId={activeConversationId}
          onSelect={selectConversation}
          onCreateConversation={handleCreateConversation}
          isCreating={isCreating}
          onDeselect={() => selectConversation(null)}
        />
      </div>

      {/* Main: chat — key forces full remount on conversation change */}
      <div className="flex-1 min-w-0">
        <ChatPanel
          key={chatKey}
          conversationId={activeConversationId}
          onCreateConversation={handleCreateConversation}
          isCreating={isCreating}
        />
      </div>
    </div>
  )
}
