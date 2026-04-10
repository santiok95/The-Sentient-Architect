'use client'

import { Sidebar } from './Sidebar'
import { Topbar } from './Topbar'
import { ConsultantPanel } from './ConsultantPanel'

interface AppShellProps {
  children: React.ReactNode
}

/**
 * AppShell — client component that wires up the 3-column layout:
 *   [Sidebar 240px] | [Topbar + main content flex-1] | [ConsultantPanel 380px]
 *
 * ConsultantPanel is inline on xl+ screens and absolute overlay below xl.
 * Sidebar is always visible on md+ and becomes a Sheet on mobile.
 */
export function AppShell({ children }: AppShellProps) {
  return (
    <div className="flex h-screen overflow-hidden bg-background relative">
      <Sidebar />
      <div className="flex-1 flex flex-col overflow-hidden min-w-0">
        <Topbar />
        <main className="flex-1 overflow-y-auto p-6">{children}</main>
      </div>
      <ConsultantPanel />
    </div>
  )
}
