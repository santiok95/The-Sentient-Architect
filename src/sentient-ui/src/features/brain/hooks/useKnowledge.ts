'use client'

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/lib/api-client'

// ─── Types ────────────────────────────────────────────────────────────────────

export interface KnowledgeItem {
  id: string
  title: string
  type: 'Article' | 'Note' | 'Documentation' | 'Repository'
  scope: 'Personal' | 'Shared'
  processingStatus: 'Pending' | 'Processing' | 'Completed' | 'Failed'
  hasEmbeddings: boolean
  sourceUrl?: string
  tags: string[]
  createdAt: string
  updatedAt: string
}

export interface KnowledgeListResponse {
  items: KnowledgeItem[]
  totalCount: number
  page: number
  pageSize: number
}

export interface Tag {
  name: string
  count: number
}

// ─── Query Keys ───────────────────────────────────────────────────────────────

export const KNOWLEDGE_KEYS = {
  all: ['knowledge'] as const,
  list: (page: number, pageSize: number, search: string, type: string) =>
    ['knowledge', 'list', page, pageSize, search, type] as const,
  tags: () => ['knowledge', 'tags'] as const,
}

// ─── Hooks ────────────────────────────────────────────────────────────────────

export function useKnowledgeItems(
  page = 1,
  pageSize = 20,
  search = '',
  type = '',
) {
  return useQuery({
    queryKey: KNOWLEDGE_KEYS.list(page, pageSize, search, type),
    queryFn: async () => {
      const params = new URLSearchParams({
        page: String(page),
        pageSize: String(pageSize),
        ...(search && { search }),
        ...(type && { type }),
      })
      const res = await apiClient.get<KnowledgeListResponse>(`/api/v1/knowledge?${params.toString()}`)
      if (!res.ok) throw new Error(res.error ?? 'Error fetching knowledge')
      return res.data ?? { items: [], totalCount: 0, page, pageSize }
    },
    placeholderData: (prev) => prev,
  })
}

export function useKnowledgeTags() {
  return useQuery({
    queryKey: KNOWLEDGE_KEYS.tags(),
    queryFn: async () => {
      const res = await apiClient.get<Tag[]>('/api/v1/tags')
      if (!res.ok) throw new Error(res.error ?? 'Error fetching tags')
      return res.data
    },
    staleTime: 5 * 60 * 1000,
  })
}

export function useKnowledgeSearch() {
  return useMutation({
    mutationFn: async (input: { query: string; maxResults?: number; includeShared?: boolean }) => {
      const res = await apiClient.post<KnowledgeItem[]>('/api/v1/knowledge/search', input)
      if (!res.ok) throw new Error(res.error ?? 'Error during search')
      return res.data
    },
  })
}

export function useInvalidateKnowledge() {
  const qc = useQueryClient()
  return () => qc.invalidateQueries({ queryKey: KNOWLEDGE_KEYS.all })
}
