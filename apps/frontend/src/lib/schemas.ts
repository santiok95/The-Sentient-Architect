/**
 * lib/schemas.ts
 * Single source of truth for all Zod validation schemas.
 * Import from here in both Server Actions and client-side forms.
 * Never duplicate schema logic across the codebase.
 */
import { z } from 'zod'

// ─── Auth ──────────────────────────────────────────────────────────────────────

export const loginSchema = z.object({
  email: z.string().email('Email inválido'),
  password: z.string().min(8, 'Contraseña de al menos 8 caracteres'),
})

export const registerSchema = z.object({
  displayName: z
    .string()
    .min(2, 'El nombre debe tener al menos 2 caracteres')
    .max(64),
  email: z.string().email('Email inválido'),
  password: z
    .string()
    .min(8, 'Contraseña de al menos 8 caracteres')
    .regex(/[A-Z]/, 'Debe contener al menos una mayúscula')
    .regex(/[0-9]/, 'Debe contener al menos un número'),
  confirmPassword: z.string(),
}).refine((data) => data.password === data.confirmPassword, {
  message: 'Las contraseñas no coinciden',
  path: ['confirmPassword'],
})

// ─── Knowledge / Brain ────────────────────────────────────────────────────────

export const ingestKnowledgeSchema = z.object({
  title: z.string().min(3, 'Título de al menos 3 caracteres').max(256),
  content: z.string().optional(),
  sourceUrl: z.string().url('URL inválida').optional().or(z.literal('')),
  type: z.enum(['Article', 'Note', 'Documentation', 'Repository']),
  tags: z.array(z.string().max(32)).max(10),
})

export const knowledgeSearchSchema = z.object({
  query: z.string().min(3, 'La búsqueda debe tener al menos 3 caracteres'),
  maxResults: z.number().int().min(1).max(50).default(10),
  includeShared: z.boolean().default(true),
})

export const publishRequestSchema = z.object({
  knowledgeItemId: z.string().uuid('ID inválido'),
  reason: z.string().min(10, 'Motivo requerido (mínimo 10 caracteres)').max(512),
})

// ─── Consultant / Chat ────────────────────────────────────────────────────────

export const sendMessageSchema = z.object({
  conversationId: z.string().uuid('ID de conversación inválido'),
  content: z
    .string()
    .min(1, 'El mensaje no puede estar vacío')
    .max(4096, 'Mensaje demasiado largo'),
  contextMode: z.enum(['Auto', 'RepoBound', 'StackBound', 'Generic']).optional(),
  preferredStack: z.string().max(128).optional(),
  activeRepositoryId: z.string().uuid().optional(),
})

export const createConversationSchema = z.object({
  title: z.string().max(128).optional(),
  agentType: z.enum(['Knowledge', 'Consultant', 'Radar']).default('Knowledge'),
  activeRepositoryId: z.string().uuid().optional(),
})

// ─── Guardian ────────────────────────────────────────────────────────────────

const githubRepoUrlRegex =
  /^https:\/\/github\.com\/[a-zA-Z0-9_.-]+\/[a-zA-Z0-9_.-]+(\.git)?$/

export const submitRepoSchema = z.object({
  repositoryUrl: z
    .string()
    .url('URL inválida')
    .regex(githubRepoUrlRegex, 'Debe ser una URL válida de GitHub'),
  trustLevel: z.enum(['External', 'Internal']),
  notes: z.string().max(512).optional(),
})

// ─── Profile ──────────────────────────────────────────────────────────────────

export const updateProfileSchema = z.object({
  displayName: z.string().min(2).max(64).optional(),
  preferredStack: z.array(z.string().max(32)).max(20).optional(),
  knownPatterns: z.array(z.string().max(64)).max(30).optional(),
  currentRole: z.string().max(100).optional(),
  yearsOfExperience: z.number().int().min(0).max(60).optional(),
  bio: z.string().max(1024).optional(),
})

// ─── Admin ───────────────────────────────────────────────────────────────────

export const setQuotaSchema = z.object({
  userId: z.string().uuid(),
  monthlyTokenLimit: z.number().int().min(0),
})

export const changeRoleSchema = z.object({
  userId: z.string().uuid(),
  role: z.enum(['Admin', 'User']),
})

export const reviewPublishRequestSchema = z.object({
  id: z.string().uuid('ID de solicitud inválido'),
  action: z.enum(['Approve', 'Reject']),
  rejectionReason: z.string().max(512).optional(),
}).refine(
  (data) => data.action !== 'Reject' || (data.rejectionReason && data.rejectionReason.length >= 5),
  { message: 'Razón de rechazo requerida', path: ['rejectionReason'] },
)

// ─── Type Exports (inferred from schemas) ────────────────────────────────────

export type LoginInput = z.infer<typeof loginSchema>
export type RegisterInput = z.infer<typeof registerSchema>
export type IngestKnowledgeInput = z.infer<typeof ingestKnowledgeSchema>
export type KnowledgeSearchInput = z.infer<typeof knowledgeSearchSchema>
export type SendMessageInput = z.infer<typeof sendMessageSchema>
export type CreateConversationInput = z.infer<typeof createConversationSchema>
export type AgentType = 'Knowledge' | 'Consultant' | 'Radar'
export type ContextMode = 'Auto' | 'RepoBound' | 'StackBound' | 'Generic'
export type SubmitRepoInput = z.infer<typeof submitRepoSchema>
export type UpdateProfileInput = z.infer<typeof updateProfileSchema>
export type ReviewPublishRequestInput = z.infer<typeof reviewPublishRequestSchema>
