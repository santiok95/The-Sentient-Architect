'use client'

import Link from 'next/link'
import { usePathname, useRouter } from 'next/navigation'
import { useEffect, useState } from 'react'
import {
  LayoutGrid,
  Brain,
  MessageSquare,
  Shield,
  Activity,
  Users,
  Rss,
  Settings,
  LogOut,
  ChevronUp,
} from 'lucide-react'
import { cn } from '@/lib/utils'
import { useUiStore, selectSidebarOpen, selectUser } from '@/store/ui-store'
import { Sheet, SheetContent } from '@/components/ui/sheet'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { logout } from '@/lib/auth'

// ─── Nav config ───────────────────────────────────────────────────────────────

type NavBadgeVariant = 'violet' | 'red'

interface NavItem {
  label: string
  href: string
  Icon: React.ElementType
  badge?: string
  badgeVariant?: NavBadgeVariant
}

interface NavGroup {
  section: string | null
  items: NavItem[]
}

const NAV_GROUPS: NavGroup[] = [
  {
    section: null,
    items: [{ label: 'Dashboard', href: '/', Icon: LayoutGrid }],
  },
  {
    section: 'Pillars',
    items: [
      { label: 'Knowledge Brain', href: '/brain', Icon: Brain, badge: '47', badgeVariant: 'violet' },
      { label: 'Consultant', href: '/consultant', Icon: MessageSquare, badge: '2', badgeVariant: 'violet' },
      { label: 'Code Guardian', href: '/guardian', Icon: Shield },
      { label: 'Trends Radar', href: '/trends', Icon: Activity, badge: '5', badgeVariant: 'violet' },
    ],
  },
  {
    section: 'Admin',
    items: [
      { label: 'Users', href: '/admin/users', Icon: Users },
      { label: 'Publish Requests', href: '/admin/publish-requests', Icon: Rss, badge: '3', badgeVariant: 'red' },
    ],
  },
  {
    section: 'System',
    items: [{ label: 'Settings', href: '/settings', Icon: Settings }],
  },
]

// ─── Sub-components ───────────────────────────────────────────────────────────

function NavBadge({ children, variant }: { children: React.ReactNode; variant: NavBadgeVariant }) {
  return (
    <span
      className={cn(
        'ml-auto text-[10.5px] font-semibold px-2 py-0.5 rounded-full',
        variant === 'violet' && 'bg-primary/15 text-primary',
        variant === 'red' && 'bg-destructive/15 text-destructive',
      )}
    >
      {children}
    </span>
  )
}

function UserMenu() {
  const user = useUiStore(selectUser)
  const setUser = useUiStore((s) => s.setUser)
  const router = useRouter()
  const [isPending, setIsPending] = useState(false)

  async function handleLogout() {
    setIsPending(true)
    await logout()
    setUser(null)
    router.replace('/login')
  }

  const initials = user?.displayName?.slice(0, 2).toUpperCase() ?? 'SA'

  return (
    <DropdownMenu>
      <DropdownMenuTrigger className="w-full flex items-center gap-2.5 px-1.5 py-2 rounded-md hover:bg-input transition-colors group focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-primary">
        <div className="w-[30px] h-[30px] rounded-full bg-gradient-to-br from-violet-500 to-indigo-600 flex items-center justify-center text-white text-[11px] font-bold flex-shrink-0">
          {initials}
        </div>
        <div className="flex-1 min-w-0 text-left">
          <div className="text-[13px] font-medium text-foreground truncate">
            {user?.displayName ?? '—'}
          </div>
          <div className="text-[11px] text-muted-foreground truncate">
            {user?.role ?? '—'}
          </div>
        </div>
        <ChevronUp className="w-3.5 h-3.5 text-muted-foreground flex-shrink-0 transition-transform group-data-[state=open]:rotate-180" />
      </DropdownMenuTrigger>
      <DropdownMenuContent side="top" align="start" className="w-56 font-mono">
        <div className="px-2 py-1.5">
          <p className="text-[11px] text-muted-foreground truncate">{user?.email ?? ''}</p>
        </div>
        <DropdownMenuSeparator />
        <DropdownMenuItem
          onClick={handleLogout}
          disabled={isPending}
          variant="destructive"
          className="gap-2 cursor-pointer"
        >
          <LogOut className="h-3.5 w-3.5" />
          {isPending ? 'Cerrando sesión…' : 'Cerrar sesión'}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}

function SidebarContent({ pathname }: { pathname: string }) {
  return (
    <div className="flex flex-col h-full">
      {/* Logo */}
      <div className="flex items-center gap-3 px-4 py-4 border-b border-border flex-shrink-0">
        <div className="w-[30px] h-[30px] rounded-lg bg-gradient-to-br from-violet-500 to-indigo-600 flex items-center justify-center text-white font-bold text-sm font-mono flex-shrink-0">
          S
        </div>
        <div>
          <div className="font-mono text-[13.5px] font-semibold text-foreground leading-tight">
            Sentient Architect
          </div>
          <div className="font-mono text-[9.5px] tracking-[0.8px] uppercase text-muted-foreground leading-tight">
            Knowledge Platform
          </div>
        </div>
      </div>

      {/* Nav */}
      <nav className="flex-1 overflow-y-auto py-3 px-2" aria-label="Main navigation">
        {NAV_GROUPS.map((group, groupIdx) => (
          <div key={groupIdx}>
            {group.section && (
              <p className="font-mono text-[10px] uppercase tracking-[0.8px] text-muted-foreground px-2 py-2 mt-1">
                {group.section}
              </p>
            )}
            {group.items.map((item) => {
              const isActive =
                item.href === '/' ? pathname === '/' : pathname.startsWith(item.href)
              return (
                <Link
                  key={item.href}
                  href={item.href}
                  className={cn(
                    'flex items-center gap-2.5 px-2.5 py-[7px] rounded-md font-mono text-[13px] transition-colors',
                    isActive
                      ? 'bg-primary/10 text-primary font-medium'
                      : 'text-muted-foreground hover:bg-input hover:text-foreground',
                  )}
                >
                  <item.Icon className="w-4 h-4 flex-shrink-0" />
                  {item.label}
                  {item.badge && item.badgeVariant && (
                    <NavBadge variant={item.badgeVariant}>{item.badge}</NavBadge>
                  )}
                </Link>
              )
            })}
          </div>
        ))}
      </nav>

      {/* Footer — user menu */}
      <div className="border-t border-border p-3 flex-shrink-0">
        <UserMenu />
      </div>
    </div>
  )
}

// ─── Main export ──────────────────────────────────────────────────────────────

export function Sidebar() {
  const pathname = usePathname()
  const sidebarOpen = useUiStore(selectSidebarOpen)
  const setSidebarOpen = useUiStore((s) => s.setSidebarOpen)
  const [isMounted, setIsMounted] = useState(false)

  useEffect(() => {
    setIsMounted(true)
  }, [])

  // Auto-close mobile sidebar on navigation
  useEffect(() => {
    setSidebarOpen(false)
  }, [pathname, setSidebarOpen])

  return (
    <>
      {/* Desktop sidebar — always visible on md+ via CSS */}
      <aside className="hidden md:flex flex-col w-60 flex-shrink-0 bg-sidebar border-r border-border h-screen">
        <SidebarContent pathname={pathname} />
      </aside>

      {/* Mobile sidebar — Sheet, rendered only after client mount to avoid hydration mismatch */}
      {isMounted && (
        <Sheet open={sidebarOpen} onOpenChange={setSidebarOpen}>
          <SheetContent
            side="left"
            className="p-0 w-60 bg-sidebar border-r border-border [&>button]:hidden"
          >
            <SidebarContent pathname={pathname} />
          </SheetContent>
        </Sheet>
      )}
    </>
  )
}
