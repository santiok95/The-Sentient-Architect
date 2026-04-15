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

// ─── Query Keys ───────────────────────────────────────────────────────────────

export const KNOWLEDGE_KEYS = {
  all: ['knowledge'] as const,
  list: (page: number, pageSize: number, search: string, type: string) =>
    ['knowledge', 'list', page, pageSize, search, type] as const,
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


export function useKnowledgeSearch() {
  return useMutation({
    mutationFn: async (input: { query: string; maxResults?: number; includeShared?: boolean }) => {
      const params = new URLSearchParams({
        q: input.query,
        ...(input.maxResults !== undefined && { maxResults: String(input.maxResults) }),
        ...(input.includeShared !== undefined && { includeShared: String(input.includeShared) }),
      })
      const res = await apiClient.get<{ results: KnowledgeItem[]; totalFound: number }>(
        `/api/v1/knowledge/search?${params.toString()}`,
      )
      if (!res.ok) throw new Error(res.error ?? 'Error during search')
      return res.data?.results ?? []
    },
  })
}

export function useInvalidateKnowledge() {
  const qc = useQueryClient()
  return () => qc.invalidateQueries({ queryKey: KNOWLEDGE_KEYS.all })
}
