import { describe, it, expect } from 'vitest'
import { createConversationSchema, sendMessageSchema } from '@/lib/schemas'

describe('createConversationSchema', () => {
  it('accepts empty input with defaults', () => {
    const result = createConversationSchema.safeParse({})
    expect(result.success).toBe(true)
    if (result.success) {
      expect(result.data.agentType).toBe('Knowledge')
    }
  })

  it('accepts title with explicit agentType', () => {
    const result = createConversationSchema.safeParse({
      title: 'Clean Architecture con .NET 9',
      agentType: 'Consultant',
    })
    expect(result.success).toBe(true)
    if (result.success) {
      expect(result.data.title).toBe('Clean Architecture con .NET 9')
      expect(result.data.agentType).toBe('Consultant')
    }
  })

  it('rejects title longer than 128 characters', () => {
    const result = createConversationSchema.safeParse({
      title: 'A'.repeat(129),
    })
    expect(result.success).toBe(false)
  })

  it('accepts both valid agentTypes', () => {
    const types = ['Knowledge', 'Consultant'] as const
    for (const agentType of types) {
      const result = createConversationSchema.safeParse({ agentType })
      expect(result.success, `agentType ${agentType} should be valid`).toBe(true)
    }
  })

  it('rejects invalid agentType', () => {
    const result = createConversationSchema.safeParse({ agentType: 'Guardian' })
    expect(result.success).toBe(false)
  })

  it('accepts optional activeRepositoryId as UUID', () => {
    const result = createConversationSchema.safeParse({
      agentType: 'Consultant',
      activeRepositoryId: '550e8400-e29b-41d4-a716-446655440000',
    })
    expect(result.success).toBe(true)
  })

  it('rejects non-UUID activeRepositoryId', () => {
    const result = createConversationSchema.safeParse({
      agentType: 'Consultant',
      activeRepositoryId: 'repo-001',
    })
    expect(result.success).toBe(false)
  })
})

describe('sendMessageSchema', () => {
  it('rejects empty content', () => {
    const result = sendMessageSchema.safeParse({
      conversationId: '550e8400-e29b-41d4-a716-446655440000',
      content: '',
    })
    expect(result.success).toBe(false)
  })

  it('rejects non-UUID conversationId', () => {
    const result = sendMessageSchema.safeParse({
      conversationId: 'conv-001',
      content: 'Hello, how do I implement CQRS?',
    })
    expect(result.success).toBe(false)
  })

  it('rejects content longer than 4096 characters', () => {
    const result = sendMessageSchema.safeParse({
      conversationId: '550e8400-e29b-41d4-a716-446655440000',
      content: 'A'.repeat(4097),
    })
    expect(result.success).toBe(false)
  })

  it('accepts valid message without contextMode', () => {
    const result = sendMessageSchema.safeParse({
      conversationId: '550e8400-e29b-41d4-a716-446655440000',
      content: 'How do I implement CQRS in .NET 9?',
    })
    expect(result.success).toBe(true)
    if (result.success) {
      expect(result.data.contextMode).toBeUndefined()
    }
  })

  it('accepts valid message with explicit contextMode', () => {
    const result = sendMessageSchema.safeParse({
      conversationId: '550e8400-e29b-41d4-a716-446655440000',
      content: 'Analyze the repository architecture.',
      contextMode: 'RepoBound',
    })
    expect(result.success).toBe(true)
    if (result.success) {
      expect(result.data.contextMode).toBe('RepoBound')
    }
  })
})
