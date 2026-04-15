'use client'

import { useTheme } from 'next-themes'
import { usePathname } from 'next/navigation'
import { Sun, Moon, Bell, MessageSquare, Menu, Search } from 'lucide-react'
import { useUiStore } from '@/store/ui-store'
import { cn } from '@/lib/utils'

// ─── Path → title map ─────────────────────────────────────────────────────────

const PATH_TITLES: Record<string, string> = {
  '/': 'Dashboard',
  '/brain': 'Knowledge Brain',
  '/consultant': 'Consultant',
  '/guardian': 'Code Guardian',
  '/trends': 'Trends Radar',
  // TODO: Uncomment when /admin/users is implemented
  // '/admin/users': 'Users',
  '/admin/publish-requests': 'Publish Requests',
  // TODO: Uncomment when /settings is implemented
  // '/settings': 'Settings',
}

function usePageTitle(): string {
  const pathname = usePathname()
  // Match exact path, then longest prefix
  if (PATH_TITLES[pathname]) return PATH_TITLES[pathname]
  const match = Object.keys(PATH_TITLES)
    .filter((p) => p !== '/' && pathname.startsWith(p))
    .sort((a, b) => b.length - a.length)[0]
  return match ? PATH_TITLES[match] : 'Dashboard'
}

// ─── Topbar ───────────────────────────────────────────────────────────────────

export function Topbar() {
  const title = usePageTitle()
  const { resolvedTheme, setTheme } = useTheme()
  const toggleConsultantPanel = useUiStore((s) => s.toggleConsultantPanel)
  const toggleSidebar = useUiStore((s) => s.toggleSidebar)
  const setCommandPaletteOpen = useUiStore((s) => s.setCommandPaletteOpen)
  const convState = useUiStore((s) => s.hubStatus['conversation']?.state)

  return (
    <header className="flex items-center gap-3 h-14 px-4 border-b border-border bg-sidebar flex-shrink-0">
      {/* Mobile hamburger — only visible below md */}
      <button
        onClick={toggleSidebar}
        aria-label="Toggle navigation"
        className="md:hidden w-[34px] h-[34px] rounded-lg flex items-center justify-center text-muted-foreground hover:bg-input hover:text-foreground transition-colors"
      >
        <Menu className="w-[17px] h-[17px]" />
      </button>

      {/* Page title */}
      <span className="font-mono text-[15px] font-semibold flex-1 text-foreground">
        {title}
      </span>

      {/* Search box — hidden on mobile */}
      <button
        onClick={() => setCommandPaletteOpen(true)}
        aria-label="Search knowledge (cmd+k)"
        className="hidden sm:flex items-center gap-2 bg-input border border-border rounded-lg px-3 py-1.5 text-muted-foreground text-[13px] w-[220px] hover:border-primary transition-colors"
      >
        <Search className="w-3.5 h-3.5 flex-shrink-0" />
        <span className="flex-1 text-left">Search knowledge...</span>
        <kbd className="ml-auto text-[10px] bg-border text-muted-foreground px-1.5 py-0.5 rounded font-mono">
          ⌘K
        </kbd>
      </button>

      {/* Theme toggle */}
      <button
        onClick={() => setTheme(resolvedTheme === 'dark' ? 'light' : 'dark')}
        aria-label="Toggle theme"
        className="w-[34px] h-[34px] rounded-lg flex items-center justify-center text-muted-foreground hover:bg-input hover:text-foreground transition-colors"
      >
        {resolvedTheme === 'dark' ? (
          <Sun className="w-[17px] h-[17px]" />
        ) : (
          <Moon className="w-[17px] h-[17px]" />
        )}
      </button>

      {/* Notifications */}
      <button
        aria-label="Notifications"
        className="relative w-[34px] h-[34px] rounded-lg flex items-center justify-center text-muted-foreground hover:bg-input hover:text-foreground transition-colors"
      >
        <Bell className="w-[17px] h-[17px]" />
        <span className="absolute top-1.5 right-1.5 w-1.5 h-1.5 rounded-full bg-primary border-2 border-sidebar" />
      </button>

      {/* Consultant toggle button */}
      <button
        onClick={toggleConsultantPanel}
        aria-label="Toggle consultant panel"
        className="flex items-center gap-2 bg-primary text-primary-foreground px-3.5 py-[7px] rounded-lg text-[13px] font-semibold hover:bg-primary/90 hover:shadow-[0_4px_14px_rgba(139,92,246,0.35)] transition-all whitespace-nowrap"
      >
        <MessageSquare className="w-[15px] h-[15px]" />
        Consultant
        <span className={cn(
          'w-1.5 h-1.5 rounded-full ml-0.5',
          convState === 'Connected' && 'bg-emerald-400',
          convState === 'Reconnecting' && 'bg-amber-400 animate-pulse',
          convState === 'Connecting' && 'bg-sky-400 animate-pulse',
          (!convState || convState === 'Disconnected' || convState === 'Disconnecting') && 'bg-red-400',
        )} />
      </button>
    </header>
  )
}
