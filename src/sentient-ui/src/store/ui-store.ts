/**
 * store/ui-store.ts
 * Zustand store for ALL UI-only visual state.
 * No server data here — that lives in TanStack Query.
 *
 * ⚠️  SINGLETON RULE: SignalR connection state is tracked here,
 * but connections are never instantiated here. Use lib/signalr.ts for that.
 */
'use client'

import { create } from 'zustand'
import { persist, createJSONStorage } from 'zustand/middleware'
import type { HubConnectionState } from '@microsoft/signalr'
import type { AuthUser } from '@/lib/auth'

// ─── Types ────────────────────────────────────────────────────────────────────

type Theme = 'dark' | 'light' | 'system'

interface HubStatus {
  state: HubConnectionState | 'Unknown'
  lastErrorAt?: string
}

interface UiState {
  // Layout
  sidebarOpen: boolean
  consultantPanelOpen: boolean

  // Theme
  activeTheme: Theme

  // Auth (persisted)
  user: AuthUser | null

  // SignalR status (UI indicators only — no connection objects)
  hubStatus: Record<string, HubStatus>

  // Command palette
  commandPaletteOpen: boolean
}

interface UiActions {
  // Layout
  toggleSidebar: () => void
  setSidebarOpen: (open: boolean) => void
  toggleConsultantPanel: () => void
  setConsultantPanelOpen: (open: boolean) => void

  // Theme
  setTheme: (theme: Theme) => void

  // Auth
  setUser: (user: AuthUser | null) => void

  // Hub status
  setHubStatus: (hubName: string, status: HubStatus) => void

  // Command palette
  setCommandPaletteOpen: (open: boolean) => void
}

// ─── Store ────────────────────────────────────────────────────────────────────

export const useUiStore = create<UiState & UiActions>()(
  persist(
    (set) => ({
      // ── Layout ──────────────────────────────────────────────────────────────
      sidebarOpen: true,
      consultantPanelOpen: false,

      toggleSidebar: () => set((s) => ({ sidebarOpen: !s.sidebarOpen })),
      setSidebarOpen: (open) => set({ sidebarOpen: open }),

      toggleConsultantPanel: () =>
        set((s) => ({ consultantPanelOpen: !s.consultantPanelOpen })),
      setConsultantPanelOpen: (open) => set({ consultantPanelOpen: open }),

      // ── Theme ────────────────────────────────────────────────────────────────
      activeTheme: 'dark',
      setTheme: (theme) => set({ activeTheme: theme }),

      // ── Auth ─────────────────────────────────────────────────────────────────
      user: null,
      setUser: (user) => set({ user }),

      // ── Hub Status ───────────────────────────────────────────────────────────
      hubStatus: {},
      setHubStatus: (hubName, status) =>
        set((s) => ({
          hubStatus: { ...s.hubStatus, [hubName]: status },
        })),

      // ── Command Palette ──────────────────────────────────────────────────────
      commandPaletteOpen: false,
      setCommandPaletteOpen: (open) => set({ commandPaletteOpen: open }),
    }),
    {
      name: 'sa-ui-store',
      storage: createJSONStorage(() => localStorage),
      // Only persist layout prefs + theme + user across reloads
      partialize: (state) => ({
        sidebarOpen: state.sidebarOpen,
        activeTheme: state.activeTheme,
        user: state.user,
      }),
    },
  ),
)

// ─── Selectors (stable references — avoids re-render storms) ─────────────────

export const selectSidebarOpen = (s: UiState) => s.sidebarOpen
export const selectConsultantPanelOpen = (s: UiState) => s.consultantPanelOpen
export const selectActiveTheme = (s: UiState) => s.activeTheme
export const selectUser = (s: UiState) => s.user
export const selectHubStatus = (s: UiState) => s.hubStatus
