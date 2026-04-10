import { describe, it, expect } from 'vitest'
import { createConversationSchema, sendMessageSchema } from '@/lib/schemas'

describe('createConversationSchema', () => {
  it('accepts empty input with defaults', () => {
    const result = createConversationSchema.safeParse({})
    expect(result.success).toBe(true)
    if (result.success) {
      expect(result.data.mode).toBe('Auto')
    }
  })

  it('accepts title with explicit mode', () => {
    const result = createConversationSchema.safeParse({
      title: 'Clean Architecture con .NET 9',
      mode: 'StackBound',
    })
    expect(result.success).toBe(true)
    if (result.success) {
      expect(result.data.title).toBe('Clean Architecture con .NET 9')
      expect(result.data.mode).toBe('StackBound')
    }
  })

  it('rejects title longer than 128 characters', () => {
    const result = createConversationSchema.safeParse({
      title: 'A'.repeat(129),
    })
    expect(result.success).toBe(false)
  })

  it('accepts all valid modes', () => {
    const modes = ['Auto', 'RepoBound', 'StackBound', 'Generic'] as const
    for (const mode of modes) {
      const result = createConversationSchema.safeParse({ mode })
      expect(result.success, `mode ${mode} should be valid`).toBe(true)
    }
  })

  it('rejects invalid mode', () => {
    const result = createConversationSchema.safeParse({ mode: 'CustomMode' })
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

  it('accepts valid message with default mode', () => {
    const result = sendMessageSchema.safeParse({
      conversationId: '550e8400-e29b-41d4-a716-446655440000',
      content: 'How do I implement CQRS in .NET 9?',
    })
    expect(result.success).toBe(true)
    if (result.success) {
      expect(result.data.mode).toBe('Auto')
    }
  })

  it('accepts valid message with explicit mode', () => {
    const result = sendMessageSchema.safeParse({
      conversationId: '550e8400-e29b-41d4-a716-446655440000',
      content: 'Analyze the repository architecture.',
      mode: 'RepoBound',
    })
    expect(result.success).toBe(true)
    if (result.success) {
      expect(result.data.mode).toBe('RepoBound')
    }
  })
})
