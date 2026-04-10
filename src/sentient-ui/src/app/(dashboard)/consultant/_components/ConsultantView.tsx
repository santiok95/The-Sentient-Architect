'use client'

import { useState } from 'react'
import { ConversationList } from '@/features/consultant/components/ConversationList'
import { ChatPanel } from '@/features/consultant/components/ChatPanel'

export function ConsultantView() {
  const [activeConversationId, setActiveConversationId] = useState<string | null>(null)

  return (
    <div className="flex h-full">
      {/* Sidebar: conversation list */}
      <div className="w-72 shrink-0 border-r border-border bg-card/50">
        <ConversationList
          activeId={activeConversationId}
          onSelect={setActiveConversationId}
        />
      </div>

      {/* Main: chat */}
      <div className="flex-1 min-w-0">
        <ChatPanel conversationId={activeConversationId} />
      </div>
    </div>
  )
}
