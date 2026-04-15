import { AppShell } from '@/components/shared/layout/AppShell'

/**
 * Dashboard layout — RSC shell.
 * Renders the AppShell client component which handles Sidebar, Topbar, and ConsultantPanel.
 * All routes under (dashboard) are auth-protected by middleware.ts.
 */
export default function DashboardLayout({
  children,
}: {
  children: React.ReactNode
}) {
  return <AppShell>{children}</AppShell>
}
