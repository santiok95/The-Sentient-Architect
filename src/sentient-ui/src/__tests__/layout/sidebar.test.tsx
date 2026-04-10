import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { Sidebar } from '@/components/shared/layout/Sidebar'
import { useUiStore } from '@/store/ui-store'

// ─── Mocks ────────────────────────────────────────────────────────────────────

const mockUsePathname = vi.fn(() => '/')

vi.mock('next/navigation', () => ({
  usePathname: () => mockUsePathname(),
}))

vi.mock('next/link', () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}))

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('Sidebar', () => {
  beforeEach(() => {
    useUiStore.setState({ sidebarOpen: false, user: null })
  })

  it('renders section labels in the desktop sidebar', () => {
    render(<Sidebar />)
    expect(screen.getByText('Pillars')).toBeInTheDocument()
    // 'Admin' appears both as section label and as default user role — use getAllByText
    expect(screen.getAllByText('Admin').length).toBeGreaterThan(0)
    expect(screen.getByText('System')).toBeInTheDocument()
  })

  it('renders all nav items', () => {
    render(<Sidebar />)
    expect(screen.getByText('Dashboard')).toBeInTheDocument()
    expect(screen.getByText('Knowledge Brain')).toBeInTheDocument()
    expect(screen.getByText('Consultant')).toBeInTheDocument()
    expect(screen.getByText('Code Guardian')).toBeInTheDocument()
    expect(screen.getByText('Trends Radar')).toBeInTheDocument()
    expect(screen.getByText('Users')).toBeInTheDocument()
    expect(screen.getByText('Publish Requests')).toBeInTheDocument()
    expect(screen.getByText('Settings')).toBeInTheDocument()
  })

  it('renders violet badges for Knowledge Brain, Consultant, and Trends Radar', () => {
    render(<Sidebar />)
    expect(screen.getAllByText('47').length).toBeGreaterThan(0)
    expect(screen.getAllByText('2').length).toBeGreaterThan(0)
    expect(screen.getAllByText('5').length).toBeGreaterThan(0)
  })

  it('renders red badge for Publish Requests', () => {
    render(<Sidebar />)
    // Badge "3" is for Publish Requests
    const badge = screen.getAllByText('3')[0]
    expect(badge).toBeInTheDocument()
    expect(badge.className).toContain('destructive')
  })

  it('renders user card with fallback initials when no user', () => {
    render(<Sidebar />)
    expect(screen.getAllByText('SA').length).toBeGreaterThan(0)
  })

  it('renders user display name when user is set', () => {
    useUiStore.setState({
      user: { id: '1', email: 'test@test.com', displayName: 'María García', role: 'Admin', tenantId: 't1' },
    })
    render(<Sidebar />)
    expect(screen.getByText('María García')).toBeInTheDocument()
    // 'Admin' appears as both section label and user role — verify at least 2 instances
    expect(screen.getAllByText('Admin').length).toBeGreaterThanOrEqual(2)
  })

  it('renders the brand logo text', () => {
    render(<Sidebar />)
    expect(screen.getByText('Sentient Architect')).toBeInTheDocument()
    expect(screen.getByText('Knowledge Platform')).toBeInTheDocument()
  })

  it('closes the mobile sidebar when pathname changes (navigation guard)', () => {
    mockUsePathname.mockReturnValue('/')
    useUiStore.setState({ sidebarOpen: true })

    const { rerender } = render(<Sidebar />)
    expect(useUiStore.getState().sidebarOpen).toBe(false) // closed on mount via effect

    // Simulate navigation to a new route
    useUiStore.setState({ sidebarOpen: true })
    mockUsePathname.mockReturnValue('/brain')
    rerender(<Sidebar />)

    expect(useUiStore.getState().sidebarOpen).toBe(false)
  })
})
