'use client'

import { X, MessageSquare, Send } from 'lucide-react'
import { cn } from '@/lib/utils'
import { useUiStore, selectConsultantPanelOpen } from '@/store/ui-store'

export function ConsultantPanel() {
  const isOpen = useUiStore(selectConsultantPanelOpen)
  const setConsultantPanelOpen = useUiStore((s) => s.setConsultantPanelOpen)

  return (
    <aside
      aria-label="Consultant panel"
      data-open={String(isOpen)}
      className={cn(
        'flex flex-col bg-sidebar border-l border-border h-screen',
        'transition-[width,min-width,opacity] duration-200',
        // On xl+ screens: inline in flex flow. Below xl: absolute overlay.
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
        <button className="text-[10.5px] font-medium px-2 py-0.5 rounded-full bg-blue-500/15 text-blue-400 border-none cursor-pointer whitespace-nowrap">
          Dashboard
        </button>
        <button
          onClick={() => setConsultantPanelOpen(false)}
          aria-label="Close consultant panel"
          className="w-[26px] h-[26px] rounded-[6px] flex items-center justify-center text-muted-foreground hover:bg-input hover:text-foreground transition-colors"
        >
          <X className="w-3.5 h-3.5" />
        </button>
      </div>

      {/* Messages */}
      <div className="flex-1 overflow-y-auto p-4 flex flex-col gap-4">
        {/* Welcome message */}
        <div className="flex gap-2.5">
          <div className="w-[26px] h-[26px] rounded-full bg-gradient-to-br from-violet-500 to-indigo-600 flex items-center justify-center text-white text-[10px] font-bold flex-shrink-0 mt-0.5">
            SA
          </div>
          <div>
            <div className="max-w-[85%] px-3 py-2.5 bg-card border border-border rounded-[4px_12px_12px_12px] text-[13px] leading-[1.55] text-foreground">
              Hi! I&apos;m your Architecture Consultant. Ask me anything about your codebase,
              design patterns, or tech decisions.
            </div>
            <div className="text-[10.5px] text-muted-foreground mt-1">Now</div>
          </div>
        </div>
      </div>

      {/* Input area */}
      <div className="p-3 border-t border-border flex-shrink-0">
        <div className="flex items-center gap-1.5 text-[11.5px] text-muted-foreground mb-2">
          <MessageSquare className="w-3 h-3" />
          Context: Dashboard · General
        </div>
        <div className="flex items-end gap-2 bg-input border border-border rounded-[10px] px-3 py-2 focus-within:border-primary transition-colors">
          <textarea
            placeholder="Ask the Consultant..."
            rows={1}
            className="flex-1 bg-transparent border-none outline-none text-foreground text-[13px] resize-none font-[inherit] leading-[1.5] max-h-[100px] placeholder:text-muted-foreground"
          />
          <button
            aria-label="Send message"
            className="w-[30px] h-[30px] rounded-[7px] bg-primary text-primary-foreground flex items-center justify-center flex-shrink-0 hover:bg-primary/90 transition-colors"
          >
            <Send className="w-3.5 h-3.5" />
          </button>
        </div>
      </div>
    </aside>
  )
}
