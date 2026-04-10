'use client'

import { useState } from 'react'
import { X, MessageSquare } from 'lucide-react'
import { cn } from '@/lib/utils'
import { useUiStore, selectConsultantPanelOpen } from '@/store/ui-store'
import { ChatPanel } from '@/features/consultant/components/ChatPanel'
import { ConversationList } from '@/features/consultant/components/ConversationList'

export function ConsultantPanel() {
  const isOpen = useUiStore(selectConsultantPanelOpen)
  const setConsultantPanelOpen = useUiStore((s) => s.setConsultantPanelOpen)
  const [activeId, setActiveId] = useState<string | null>(null)
  const [view, setView] = useState<'chat' | 'list'>('chat')

  return (
    <aside
      aria-label="Consultant panel"
      data-open={String(isOpen)}
      className={cn(
        'flex flex-col bg-sidebar border-l border-border h-screen',
        'transition-[width,min-width,opacity] duration-200',
        'xl:relative xl:h-auto',
        'max-xl:absolute max-xl:right-0 max-xl:top-0 max-xl:z-50',
        isOpen
          ? 'w-[380px] min-w-[380px] opacity-100'
          : 'w-0 min-w-0 opacity-0 overflow-hidden pointer-events-none',
      )}
    >
      {/* Header */}
      <div className="flex items-center gap-2.5 px-4 py-3.5 border-b border-border min-h-14 flex-shrink-0">
        <div className="w-7 h-7 rounded-[7px] bg-primary/15 flex items-center justify-center flex-shrink-0">
          <MessageSquare className="w-3.5 h-3.5 text-primary" />
        </div>
        <span className="font-mono text-[13.5px] font-semibold text-foreground flex-1">
          Consultant
        </span>
        <button
          onClick={() => setView(view === 'chat' ? 'list' : 'chat')}
          className="text-[10.5px] font-medium px-2 py-0.5 rounded-full bg-blue-500/15 text-blue-400 border-none cursor-pointer whitespace-nowrap"
        >
          {view === 'chat' ? 'Conversaciones' : 'Chat'}
        </button>
        <button
          onClick={() => setConsultantPanelOpen(false)}
          aria-label="Close consultant panel"
          className="w-[26px] h-[26px] rounded-[6px] flex items-center justify-center text-muted-foreground hover:bg-input hover:text-foreground transition-colors"
        >
          <X className="w-3.5 h-3.5" />
        </button>
      </div>

      {/* Body */}
      <div className="flex-1 min-h-0 overflow-hidden">
        {view === 'list' ? (
          <ConversationList
            activeId={activeId}
            onSelect={(id) => {
              setActiveId(id)
              setView('chat')
            }}
          />
        ) : (
          <ChatPanel conversationId={activeId} />
        )}
      </div>
    </aside>
  )
}
