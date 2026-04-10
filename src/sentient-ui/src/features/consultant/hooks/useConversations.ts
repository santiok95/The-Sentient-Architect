'use client'

import { useQuery } from '@tanstack/react-query'
import { apiClient } from '@/lib/api-client'

// ─── Types ────────────────────────────────────────────────────────────────────

export interface ConversationSummary {
  id: string
  objective: string
  mode: 'Auto' | 'RepoBound' | 'StackBound' | 'Generic'
  status: 'Active' | 'Archived' | 'Completed'
  messageCount: number
  lastMessageAt?: string
  createdAt: string
}

export interface ConversationMessage {
  id: string
  role: 'User' | 'Assistant'
  content: string
  createdAt: string
  retrievedContextIds?: string[]
}

export interface ConversationDetail extends ConversationSummary {
  recentMessages: ConversationMessage[]
}

// ─── Query Keys ───────────────────────────────────────────────────────────────

export const CONVERSATION_KEYS = {
  all: ['conversations'] as const,
  list: (status?: string) => ['conversations', 'list', status ?? 'all'] as const,
  detail: (id: string) => ['conversations', 'detail', id] as const,
}

// ─── Hooks ────────────────────────────────────────────────────────────────────

export function useConversations(status?: string) {
  return useQuery({
    queryKey: CONVERSATION_KEYS.list(status),
    queryFn: async () => {
      const params = new URLSearchParams()
      if (status) params.set('status', status)
      const res = await apiClient.get<{ items: ConversationSummary[]; totalCount: number }>(
        `/api/v1/conversations${params.size ? `?${params.toString()}` : ''}`,
      )
      if (!res.ok) throw new Error(res.error ?? 'Error fetching conversations')
      return res.data
    },
  })
}

export function useConversation(id: string | null) {
  return useQuery({
    queryKey: CONVERSATION_KEYS.detail(id ?? ''),
    queryFn: async () => {
      const res = await apiClient.get<ConversationDetail>(`/api/v1/conversations/${id}`)
      if (!res.ok) throw new Error(res.error ?? 'Error fetching conversation')
      return res.data
    },
    enabled: !!id,
  })
}
