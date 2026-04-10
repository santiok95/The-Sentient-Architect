'use client'

import { useQuery } from '@tanstack/react-query'
import { apiClient } from '@/lib/api-client'

// ─── Types ────────────────────────────────────────────────────────────────────

export interface RepositorySummary {
  id: string
  knowledgeItemId: string
  gitUrl: string
  primaryLanguage?: string
  trustLevel: 'External' | 'Internal'
  stars?: number
  openIssues?: number
  lastCommitDate?: string
  processingStatus: 'Pending' | 'Processing' | 'Completed' | 'Failed'
  scope: 'Personal' | 'Shared'
  createdAt: string
}

export interface AnalysisReport {
  id: string
  analysisType: string
  overallHealthScore: number
  securityScore: number
  qualityScore: number
  maintainabilityScore: number
  findingsCount: { critical: number; high: number; medium: number; low: number }
  executedAt: string
  analysisDurationSeconds: number
}

export interface RepositoryAnalysis {
  repositoryInfo: {
    gitUrl: string
    primaryLanguage?: string
    trustLevel: string
    stars?: number
    openIssues?: number
    lastCommitDate?: string
  }
  reports: AnalysisReport[]
}

export interface Finding {
  id: string
  severity: 'Critical' | 'High' | 'Medium' | 'Low'
  category: string
  title: string
  description: string
  filePath?: string
  recommendation?: string
  isResolved: boolean
}

// ─── Query Keys ───────────────────────────────────────────────────────────────

export const REPOSITORY_KEYS = {
  all: ['repositories'] as const,
  list: () => ['repositories', 'list'] as const,
  analysis: (knowledgeItemId: string) => ['repositories', 'analysis', knowledgeItemId] as const,
  findings: (knowledgeItemId: string, reportId: string) =>
    ['repositories', 'findings', knowledgeItemId, reportId] as const,
}

// ─── Hooks ────────────────────────────────────────────────────────────────────

export function useRepositories() {
  return useQuery({
    queryKey: REPOSITORY_KEYS.list(),
    queryFn: async () => {
      const res = await apiClient.get<{ items: RepositorySummary[]; totalCount: number }>(
        '/api/v1/repositories',
      )
      if (!res.ok) throw new Error(res.error ?? 'Error fetching repositories')
      return res.data
    },
  })
}

export function useRepositoryAnalysis(knowledgeItemId: string | null) {
  return useQuery({
    queryKey: REPOSITORY_KEYS.analysis(knowledgeItemId ?? ''),
    queryFn: async () => {
      const res = await apiClient.get<RepositoryAnalysis>(
        `/api/v1/repositories/${knowledgeItemId}/analysis`,
      )
      if (!res.ok) throw new Error(res.error ?? 'Error fetching analysis')
      return res.data
    },
    enabled: !!knowledgeItemId,
  })
}

export function useFindings(knowledgeItemId: string | null, reportId: string | null) {
  return useQuery({
    queryKey: REPOSITORY_KEYS.findings(knowledgeItemId ?? '', reportId ?? ''),
    queryFn: async () => {
      const res = await apiClient.get<{ items: Finding[]; totalCount: number }>(
        `/api/v1/repositories/${knowledgeItemId}/analysis/${reportId}/findings`,
      )
      if (!res.ok) throw new Error(res.error ?? 'Error fetching findings')
      return res.data
    },
    enabled: !!knowledgeItemId && !!reportId,
  })
}
