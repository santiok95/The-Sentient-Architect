import { describe, it, expect } from 'vitest'
import {
  loginSchema,
  registerSchema,
  ingestKnowledgeSchema,
  createConversationSchema,
  sendMessageSchema,
  submitRepoSchema,
} from '@/lib/schemas'

describe('Auth schemas', () => {
  it('loginSchema: rejects invalid email', () => {
    const result = loginSchema.safeParse({ email: 'not-an-email', password: 'ValidPass1' })
    expect(result.success).toBe(false)
  })

  it('loginSchema: rejects short password', () => {
    const result = loginSchema.safeParse({ email: 'test@test.com', password: 'short' })
    expect(result.success).toBe(false)
  })

  it('loginSchema: accepts valid credentials', () => {
    const result = loginSchema.safeParse({ email: 'test@test.com', password: 'SecurePass1' })
    expect(result.success).toBe(true)
  })

  it('registerSchema: rejects mismatched passwords', () => {
    const result = registerSchema.safeParse({
      displayName: 'Jane',
      email: 'jane@test.com',
      password: 'ValidPass1',
      confirmPassword: 'Different1',
    })
    expect(result.success).toBe(false)
    if (!result.success) {
      expect(result.error.issues[0].path).toContain('confirmPassword')
    }
  })

  it('registerSchema: accepts valid registration', () => {
    const result = registerSchema.safeParse({
      displayName: 'Jane',
      email: 'jane@test.com',
      password: 'ValidPass1',
      confirmPassword: 'ValidPass1',
    })
    expect(result.success).toBe(true)
  })
})

describe('Knowledge schemas', () => {
  it('ingestKnowledgeSchema: rejects empty title', () => {
    const result = ingestKnowledgeSchema.safeParse({
      title: 'ab',
      type: 'Article',
      tags: [],
    })
    expect(result.success).toBe(false)
  })

  it('ingestKnowledgeSchema: accepts valid article', () => {
    const result = ingestKnowledgeSchema.safeParse({
      title: 'Valid Article Title',
      type: 'Article',
      tags: ['typescript', 'clean-arch'],
    })
    expect(result.success).toBe(true)
  })

  it('ingestKnowledgeSchema: rejects invalid sourceUrl', () => {
    const result = ingestKnowledgeSchema.safeParse({
      title: 'Valid Title Here',
      type: 'Note',
      tags: [],
      sourceUrl: 'not-a-url',
    })
    expect(result.success).toBe(false)
  })
})

describe('Guardian schemas', () => {
  it('submitRepoSchema: rejects non-github URL', () => {
    const result = submitRepoSchema.safeParse({
      repositoryUrl: 'https://gitlab.com/user/repo',
      trustLevel: 'External',
    })
    expect(result.success).toBe(false)
  })

  it('submitRepoSchema: accepts valid github URL', () => {
    const result = submitRepoSchema.safeParse({
      repositoryUrl: 'https://github.com/user/my-repo',
      trustLevel: 'External',
    })
    expect(result.success).toBe(true)
  })
})

describe('Consultant schemas', () => {
  it('sendMessageSchema: rejects empty message', () => {
    const result = sendMessageSchema.safeParse({
      conversationId: '550e8400-e29b-41d4-a716-446655440000',
      content: '',
    })
    expect(result.success).toBe(false)
  })

  it('sendMessageSchema: rejects invalid UUID', () => {
    const result = sendMessageSchema.safeParse({
      conversationId: 'not-a-uuid',
      content: 'Hello architect',
    })
    expect(result.success).toBe(false)
  })

  it('sendMessageSchema: contextMode is optional', () => {
    const result = sendMessageSchema.safeParse({
      conversationId: '550e8400-e29b-41d4-a716-446655440000',
      content: 'Tell me about clean architecture',
    })
    expect(result.success).toBe(true)
    if (result.success) {
      expect(result.data.contextMode).toBeUndefined()
    }
  })

  it('createConversationSchema: accepts Radar agent type', () => {
    const result = createConversationSchema.safeParse({
      title: 'Radar session',
      agentType: 'Radar',
    })

    expect(result.success).toBe(true)
  })
})
