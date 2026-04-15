import { useQuery, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/lib/api-client'
import type { PublishRequest, PagedResult } from '@/lib/api.types'

// ─── Query Keys ───────────────────────────────────────────────────────────────

export const ADMIN_KEYS = {
  publishRequests: (status?: string) => ['admin', 'publish-requests', status ?? 'all'] as const,
  users: () => ['admin', 'users'] as const,
}

// ─── Publish Requests ─────────────────────────────────────────────────────────

export function usePublishRequests(status?: string) {
  return useQuery({
    queryKey: ADMIN_KEYS.publishRequests(status),
    queryFn: async () => {
      const params = new URLSearchParams()
      if (status) params.set('status', status)
      params.set('page', '1')
      params.set('pageSize', '50')

      const result = await apiClient.get<PagedResult<PublishRequest>>(
        `/api/v1/admin/publish-requests?${params.toString()}`,
      )
      if (!result.ok) throw new Error(result.error)
      return result.data
    },
  })
}

export function useInvalidatePublishRequests() {
  const qc = useQueryClient()
  return () => qc.invalidateQueries({ queryKey: ['admin', 'publish-requests'] })
}
