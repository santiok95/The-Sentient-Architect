import { describe, it, expect, beforeEach } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { ConsultantPanel } from '@/components/shared/layout/ConsultantPanel'
import { useUiStore } from '@/store/ui-store'

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('ConsultantPanel', () => {
  beforeEach(() => {
    useUiStore.setState({ consultantPanelOpen: false })
  })

  it('renders with data-open="false" when closed', () => {
    render(<ConsultantPanel />)
    const panel = screen.getByRole('complementary', { name: /consultant panel/i })
    expect(panel).toHaveAttribute('data-open', 'false')
  })

  it('renders with data-open="true" when open', () => {
    useUiStore.setState({ consultantPanelOpen: true })
    render(<ConsultantPanel />)
    const panel = screen.getByRole('complementary', { name: /consultant panel/i })
    expect(panel).toHaveAttribute('data-open', 'true')
  })

  it('renders the welcome message when open', () => {
    useUiStore.setState({ consultantPanelOpen: true })
    render(<ConsultantPanel />)
    expect(screen.getByText(/Architecture Consultant/i)).toBeInTheDocument()
  })

  it('renders send button and textarea', () => {
    useUiStore.setState({ consultantPanelOpen: true })
    render(<ConsultantPanel />)
    expect(screen.getByRole('button', { name: /send message/i })).toBeInTheDocument()
    expect(screen.getByPlaceholderText(/ask the consultant/i)).toBeInTheDocument()
  })

  it('close button sets consultantPanelOpen to false', () => {
    useUiStore.setState({ consultantPanelOpen: true })
    render(<ConsultantPanel />)
    const closeBtn = screen.getByRole('button', { name: /close consultant panel/i })
    fireEvent.click(closeBtn)
    expect(useUiStore.getState().consultantPanelOpen).toBe(false)
  })

  it('has the panel title "Consultant"', () => {
    useUiStore.setState({ consultantPanelOpen: true })
    render(<ConsultantPanel />)
    // The title inside the panel header
    const titles = screen.getAllByText('Consultant')
    expect(titles.length).toBeGreaterThan(0)
  })
})
