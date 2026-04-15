import { describe, it, expect } from 'vitest'
import { ingestKnowledgeSchema, knowledgeSearchSchema, publishRequestSchema } from '@/lib/schemas'

describe('ingestKnowledgeSchema', () => {
  it('rejects missing title', () => {
    const result = ingestKnowledgeSchema.safeParse({
      type: 'Article',
      tags: [],
    })
    expect(result.success).toBe(false)
    if (!result.success) {
      expect(result.error.issues.some((i) => i.path.includes('title'))).toBe(true)
    }
  })

  it('rejects title shorter than 3 characters', () => {
    const result = ingestKnowledgeSchema.safeParse({
      title: 'hi',
      type: 'Article',
      tags: [],
    })
    expect(result.success).toBe(false)
  })

  it('rejects invalid type', () => {
    const result = ingestKnowledgeSchema.safeParse({
      title: 'Valid Title',
      type: 'Video',
      tags: [],
    })
    expect(result.success).toBe(false)
  })

  it('accepts a valid Article with tags', () => {
    const result = ingestKnowledgeSchema.safeParse({
      title: 'CQRS en .NET 9 con Vertical Slice',
      type: 'Article',
      sourceUrl: 'https://devblogs.microsoft.com/dotnet/cqrs',
      content: 'Contenido del artículo sobre CQRS...',
      tags: ['cqrs', 'dotnet', 'architecture'],
    })
    expect(result.success).toBe(true)
  })

  it('accepts all valid types', () => {
    const types = ['Article', 'Note', 'Documentation', 'Repository'] as const
    for (const type of types) {
      const result = ingestKnowledgeSchema.safeParse({ title: 'Test', type, tags: [] })
      expect(result.success, `type ${type} should be valid`).toBe(true)
    }
  })

  it('rejects more than 10 tags', () => {
    const result = ingestKnowledgeSchema.safeParse({
      title: 'Test',
      type: 'Note',
      tags: Array.from({ length: 11 }, (_, i) => `tag-${i}`),
    })
    expect(result.success).toBe(false)
  })

  it('rejects invalid sourceUrl', () => {
    const result = ingestKnowledgeSchema.safeParse({
      title: 'Test title here',
      type: 'Article',
      sourceUrl: 'not-a-url',
      tags: [],
    })
    expect(result.success).toBe(false)
  })
})

describe('knowledgeSearchSchema', () => {
  it('rejects empty query', () => {
    const result = knowledgeSearchSchema.safeParse({ query: '' })
    expect(result.success).toBe(false)
  })

  it('rejects query shorter than 3 characters', () => {
    const result = knowledgeSearchSchema.safeParse({ query: 'ab' })
    expect(result.success).toBe(false)
  })

  it('accepts valid search and applies defaults', () => {
    const result = knowledgeSearchSchema.safeParse({ query: 'CQRS architecture' })
    expect(result.success).toBe(true)
    if (result.success) {
      expect(result.data.maxResults).toBe(10)
      expect(result.data.includeShared).toBe(true)
    }
  })

  it('respects explicit maxResults', () => {
    const result = knowledgeSearchSchema.safeParse({ query: 'search term', maxResults: 25 })
    expect(result.success).toBe(true)
    if (result.success) expect(result.data.maxResults).toBe(25)
  })
})

describe('publishRequestSchema', () => {
  it('rejects missing knowledgeItemId', () => {
    const result = publishRequestSchema.safeParse({ reason: 'Great content!' })
    expect(result.success).toBe(false)
  })

  it('rejects non-UUID knowledgeItemId', () => {
    const result = publishRequestSchema.safeParse({
      knowledgeItemId: 'not-a-uuid',
      reason: 'Good content to share',
    })
    expect(result.success).toBe(false)
  })

  it('rejects reason shorter than 10 characters', () => {
    const result = publishRequestSchema.safeParse({
      knowledgeItemId: '550e8400-e29b-41d4-a716-446655440000',
      reason: 'Short',
    })
    expect(result.success).toBe(false)
  })

  it('accepts valid publish request', () => {
    const result = publishRequestSchema.safeParse({
      knowledgeItemId: '550e8400-e29b-41d4-a716-446655440000',
      reason: 'Este artículo sobre CQRS es muy valioso para el equipo.',
    })
    expect(result.success).toBe(true)
  })
})
