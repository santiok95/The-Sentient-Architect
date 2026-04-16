'use client'

import { useQuery } from '@tanstack/react-query'
import { apiClient } from '@/lib/api-client'

// ─── Types ────────────────────────────────────────────────────────────────────

export interface RepositorySummary {
  id: string
  gitUrl: string
  primaryLanguage?: string
  trustLevel: string
  stars?: number
  lastCommitDate?: string
  processingStatus: string
  scope: 'Personal' | 'Shared'
  createdAt: string
}

export interface AnalysisReport {
  id: string
  status: string
  summary?: string
  totalFindings: number
  criticalFindings: number
  findingsCount: { critical: number; high: number; medium: number; low: number }
  overallHealthScore: number
  securityScore: number
  qualityScore: number
  maintainabilityScore: number
  executedAt?: string
  analysisDurationSeconds: number
}

export interface RepositoryAnalysis {
  repositoryInfo: {
    gitUrl: string
    primaryLanguage?: string
    trustLevel: string
    stars?: number
    lastCommitDate?: string
  }
  reports: AnalysisReport[]
}

export interface Finding {
  id: string
  severity: 'Critical' | 'High' | 'Medium' | 'Low'
  category: string
  title: string
  description?: string
  filePath?: string
  isResolved: boolean
}

// Raw shape from GetAnalysisReportResponse.Findings
interface RawFinding {
  id?: string
  severity: string
  category: string
  message?: string
  title?: string
  filePath?: string
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
      const raw = res.data
      if (!raw) return { items: [] as RepositorySummary[], totalCount: 0 }
      // Backend may return a plain array or a paged object
      const items: RepositorySummary[] = Array.isArray(raw)
        ? (raw as RepositorySummary[])
        : raw.items ?? []
      return { items, totalCount: raw.totalCount ?? items.length }
    },
  })
}

export function useRepositoryAnalysis(repositoryId: string | null) {
  return useQuery({
    queryKey: REPOSITORY_KEYS.analysis(repositoryId ?? ''),
    queryFn: async () => {
      const res = await apiClient.get<RepositoryAnalysis>(
        `/api/v1/repositories/${repositoryId}/analysis`,
      )
      if (!res.ok) throw new Error(res.error ?? 'Error fetching analysis')
      return res.data
    },
    enabled: !!repositoryId,
    // Poll every 4 s while any report is still in progress — covers the case
    // where the SignalR ReceiveComplete event is missed (network drop, page reload, etc.)
    refetchInterval: (query) => {
      const reports = query.state.data?.reports ?? []
      const hasPending = reports.some(
        (r) => r.status === 'InProgress' || r.status === 'Pending',
      )
      return hasPending ? 4000 : false
    },
  })
}

export function useFindings(repositoryId: string | null, reportId: string | null) {
  return useQuery({
    queryKey: REPOSITORY_KEYS.findings(repositoryId ?? '', reportId ?? ''),
    queryFn: async () => {
      // Backend endpoint: GET /api/v1/repositories/reports/{reportId}
      const res = await apiClient.get<{ items: Finding[]; totalCount: number }>(
        `/api/v1/repositories/reports/${reportId}`,
      )
      if (!res.ok) throw new Error(res.error ?? 'Error fetching findings')
      // Backend returns GetAnalysisReportResponse with a Findings array; normalize it
      const data = res.data as unknown as { findings?: RawFinding[]; items?: Finding[]; totalCount?: number }
      const rawFindings: RawFinding[] = data?.findings ?? []
      const items: Finding[] = rawFindings.map((f) => ({
        id: f.id ?? crypto.randomUUID(),
        severity: f.severity as Finding['severity'],
        category: f.category,
        title: f.message ?? f.title ?? '',
        description: undefined,
        filePath: f.filePath,
        isResolved: false,
      }))
      return { items, totalCount: data?.totalCount ?? items.length }
    },
    enabled: !!repositoryId && !!reportId,
  })
}
