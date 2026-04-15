import { describe, it, expect } from 'vitest'
import { submitRepoSchema } from '@/lib/schemas'

describe('submitRepoSchema', () => {
  it('rejects non-GitHub URLs', () => {
    const invalidUrls = [
      'https://gitlab.com/org/repo',
      'https://bitbucket.org/org/repo',
      'https://example.com/repo',
      'not-a-url-at-all',
    ]
    for (const url of invalidUrls) {
      const result = submitRepoSchema.safeParse({ repositoryUrl: url })
      expect(result.success, `${url} should be rejected`).toBe(false)
    }
  })

  it('accepts valid github.com URLs', () => {
    const validUrls = [
      'https://github.com/dotnet/aspire',
      'https://github.com/microsoft/semantic-kernel',
      'https://github.com/org/my-repo.git',
      'https://github.com/user-name/repo_name',
    ]
    for (const url of validUrls) {
      const result = submitRepoSchema.safeParse({ repositoryUrl: url })
      expect(result.success, `${url} should be valid`).toBe(true)
    }
  })

  it('defaults trustLevel to External', () => {
    const result = submitRepoSchema.safeParse({
      repositoryUrl: 'https://github.com/dotnet/aspire',
    })
    expect(result.success).toBe(true)
    if (result.success) {
      expect(result.data.trustLevel).toBe('External')
    }
  })

  it('accepts Internal trustLevel', () => {
    const result = submitRepoSchema.safeParse({
      repositoryUrl: 'https://github.com/my-org/internal-repo',
      trustLevel: 'Internal',
    })
    expect(result.success).toBe(true)
    if (result.success) {
      expect(result.data.trustLevel).toBe('Internal')
    }
  })

  it('rejects invalid trustLevel', () => {
    const result = submitRepoSchema.safeParse({
      repositoryUrl: 'https://github.com/org/repo',
      trustLevel: 'Public',
    })
    expect(result.success).toBe(false)
  })

  it('accepts optional notes field', () => {
    const result = submitRepoSchema.safeParse({
      repositoryUrl: 'https://github.com/dotnet/aspire',
      trustLevel: 'Internal',
      notes: 'Microsoft official repo for distributed .NET apps',
    })
    expect(result.success).toBe(true)
  })

  it('accepts without notes', () => {
    const result = submitRepoSchema.safeParse({
      repositoryUrl: 'https://github.com/dotnet/aspire',
    })
    expect(result.success).toBe(true)
  })
})
