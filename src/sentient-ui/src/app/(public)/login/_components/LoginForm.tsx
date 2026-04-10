'use client'

import { useState } from 'react'
import { useRouter, useSearchParams } from 'next/navigation'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { login } from '@/lib/auth'
import { Loader2 } from 'lucide-react'

export function LoginForm() {
  const router = useRouter()
  const searchParams = useSearchParams()
  const from = searchParams.get('from') ?? '/'

  const [error, setError] = useState<string | null>(null)
  const [isPending, setIsPending] = useState(false)

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault()
    setError(null)
    setIsPending(true)

    const formData = new FormData(e.currentTarget)
    const email = formData.get('email') as string
    const password = formData.get('password') as string

    const result = await login({ email, password })

    if (!result.ok) {
      setError(result.error)
      setIsPending(false)
      return
    }

    router.push(from)
    router.refresh()
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      <div className="space-y-1.5">
        <Label htmlFor="email" className="text-xs font-medium">
          Email
        </Label>
        <Input
          id="email"
          type="email"
          name="email"
          placeholder="dev@example.com"
          autoComplete="email"
          required
          disabled={isPending}
        />
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="password" className="text-xs font-medium">
          Password
        </Label>
        <Input
          id="password"
          type="password"
          name="password"
          placeholder="••••••••"
          autoComplete="current-password"
          required
          disabled={isPending}
        />
      </div>

      {error && (
        <p className="text-xs text-destructive bg-destructive/10 border border-destructive/20 rounded-md px-3 py-2">
          {error}
        </p>
      )}

      <Button type="submit" className="w-full" size="sm" disabled={isPending}>
        {isPending ? (
          <>
            <Loader2 className="mr-2 h-3.5 w-3.5 animate-spin" />
            Signing in…
          </>
        ) : (
          'Sign In'
        )}
      </Button>
    </form>
  )
}
