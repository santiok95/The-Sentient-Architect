import { useQuery, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/lib/api-client'

export interface MyPublishRequest {
  id: string
  knowledgeItemId: string
  knowledgeItemTitle: string
  knowledgeItemType: string
  requestReason?: string
  status: 'Pending' | 'Approved' | 'Rejected'
  createdAt: string
  reviewedAt?: string
  rejectionReason?: string
}

export interface MyPublishRequestsResponse {
  items: MyPublishRequest[]
  totalCount: number
  page: number
  pageSize: number
}

export const MY_PUBLISH_REQUESTS_KEY = ['knowledge', 'my-publish-requests'] as const

export function useMyPublishRequests(page = 1, pageSize = 20) {
  return useQuery({
    queryKey: [...MY_PUBLISH_REQUESTS_KEY, page, pageSize],
    queryFn: async () => {
      const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
      const res = await apiClient.get<MyPublishRequestsResponse>(
        `/api/v1/knowledge/my-publish-requests?${params.toString()}`,
      )
      if (!res.ok) throw new Error(res.error ?? 'Error al cargar solicitudes')
      return res.data ?? { items: [], totalCount: 0, page, pageSize }
    },
  })
}

export function useInvalidateMyPublishRequests() {
  const qc = useQueryClient()
  return () => qc.invalidateQueries({ queryKey: MY_PUBLISH_REQUESTS_KEY })
}
