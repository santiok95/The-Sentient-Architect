/**
 * lib/api.types.ts
 * AUTO-GENERATED — DO NOT EDIT MANUALLY.
 * Regenerate with: npm run types:generate
 * Requires the .NET API to be running on NEXT_PUBLIC_API_URL.
 *
 * Placeholder types until the generator runs against the live backend.
 * These are minimal stubs matching the API contracts defined in docs/API_CONTRACTS.md.
 */

// ─── Knowledge ────────────────────────────────────────────────────────────────

export type KnowledgeType = 'Article' | 'Note' | 'Documentation' | 'Repository'
export type KnowledgeStatus = 'Pending' | 'Processing' | 'Completed' | 'Failed'
export type KnowledgeScope = 'Personal' | 'Shared'

export interface KnowledgeItem {
  id: string
  title: string
  type: KnowledgeType
  status: KnowledgeStatus
  scope: KnowledgeScope
  tags: string[]
  userId: string
  tenantId: string
  createdAt: string
  updatedAt: string
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

export interface SearchResult {
  item: KnowledgeItem
  score: number
  excerpt: string
}

// ─── Conversations ────────────────────────────────────────────────────────────

export type ConversationMode = 'Auto' | 'RepoBound' | 'StackBound' | 'Generic'
export type ConversationStatus = 'Active' | 'Archived'
export type MessageRole = 'User' | 'Assistant'

export interface Conversation {
  id: string
  title: string
  status: ConversationStatus
  mode: ConversationMode
  messageCount: number
  createdAt: string
  updatedAt: string
}

export interface ConversationMessage {
  id: string
  conversationId: string
  content: string
  role: MessageRole
  tokensUsed?: number
  createdAt: string
}

// ─── Guardian ────────────────────────────────────────────────────────────────

export type TrustLevel = 'External' | 'Internal'
export type AnalysisStatus = 'Pending' | 'Running' | 'Completed' | 'Failed'

export interface Repository {
  id: string
  url: string
  name: string
  trustLevel: TrustLevel
  lastAnalysisStatus: AnalysisStatus
  createdAt: string
}

// ─── Trends ───────────────────────────────────────────────────────────────────

export type TractionLevel = 'Emerging' | 'Growing' | 'Mainstream' | 'Declining'

export interface Trend {
  id: string
  name: string
  category: string
  tractionLevel: TractionLevel
  relevanceScore: number
  summary: string | null
  sources: string[]
  lastUpdatedAt: string
  starCount: number | null
  gitHubUrl: string | null
}

export interface TrendSnapshot {
  tractionLevel: TractionLevel
  sentimentScore: number
  snapshotDate: string
  notes: string | null
}

// ─── Profile ──────────────────────────────────────────────────────────────────

export interface UserProfile {
  userId: string
  displayName: string
  currentRole?: string
  yearsOfExperience?: number
  bio?: string
  preferredStack: string[]
  knownPatterns: string[]
  totalTokensUsed: number
  monthlyTokenLimit: number
}

// ─── Admin ───────────────────────────────────────────────────────────────────

export type PublishRequestStatus = 'Pending' | 'Approved' | 'Rejected'

export interface PublishRequest {
  id: string
  knowledgeItem: {
    id: string
    title: string
    type: string
    summary?: string
  }
  requestedBy: {
    id: string
    displayName: string
    role: string
  }
  requestReason?: string
  status: PublishRequestStatus
  createdAt: string
  reviewedAt?: string
}

export interface AdminUser {
  id: string
  email: string
  displayName: string
  role: string
  createdAt: string
  todayTokenUsage: number
}
