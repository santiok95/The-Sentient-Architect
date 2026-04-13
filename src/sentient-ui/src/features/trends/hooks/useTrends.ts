import { useQuery } from '@tanstack/react-query'
import { apiClient } from '@/lib/api-client'
import type { Trend, TrendSnapshot, PagedResult } from '@/lib/api.types'

// ─── Query Keys ───────────────────────────────────────────────────────────────

export const TRENDS_KEYS = {
  all: ['trends'] as const,
  list: (filters?: TrendsFilters) => ['trends', 'list', filters ?? {}] as const,
  snapshots: (id: string) => ['trends', 'snapshots', id] as const,
}

// ─── Types ────────────────────────────────────────────────────────────────────

export interface TrendsFilters {
  category?: string
  traction?: string
  minRelevance?: number
  page?: number
  pageSize?: number
}

// ─── Hooks ────────────────────────────────────────────────────────────────────

export function useTrends(filters: TrendsFilters = {}) {
  return useQuery({
    queryKey: TRENDS_KEYS.list(filters),
    queryFn: async () => {
      const params = new URLSearchParams()
      if (filters.category) params.set('category', filters.category)
      if (filters.traction) params.set('traction', filters.traction)
      if (filters.minRelevance) params.set('minRelevance', String(filters.minRelevance))
      params.set('page', String(filters.page ?? 1))
      params.set('pageSize', String(filters.pageSize ?? 20))

      const result = await apiClient.get<PagedResult<Trend>>(
        `/api/v1/trends?${params.toString()}`,
      )
      if (!result.ok) throw new Error(result.error)
      return result.data
    },
    staleTime: 5 * 60 * 1000, // Trends don't change by the minute
  })
}

export function useTrendSnapshots(id: string | null) {
  return useQuery({
    queryKey: TRENDS_KEYS.snapshots(id ?? ''),
    queryFn: async () => {
      const result = await apiClient.get<{ trend: { id: string; name: string }; snapshots: TrendSnapshot[] }>(
        `/api/v1/trends/${id}/snapshots`,
      )
      if (!result.ok) throw new Error(result.error)
      return result.data
    },
    enabled: !!id,
  })
}

// ─── Category + Traction constants (for filter dropdowns) ────────────────────

export const TREND_CATEGORIES = [
  'Framework', 'Language', 'Tool', 'Pattern', 'Platform', 'Library',
  'BestPractice', 'Innovation', 'Architecture', 'DevOps', 'Testing',
] as const

export const TRACTION_LEVELS = ['Emerging', 'Growing', 'Mainstream', 'Declining'] as const
