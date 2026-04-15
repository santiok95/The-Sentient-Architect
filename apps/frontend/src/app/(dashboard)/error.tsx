'use client'

import { useEffect } from 'react'
import { AlertTriangle, RefreshCw } from 'lucide-react'
import { Button } from '@/components/ui/button'

/**
 * Dashboard error boundary — required 'use client' by Next.js.
 * Catches runtime errors within the (dashboard) route segment.
 */
export default function DashboardError({
  error,
  reset,
}: {
  error: Error & { digest?: string }
  reset: () => void
}) {
  useEffect(() => {
    console.error('[DashboardError]', error)
  }, [error])

  return (
    <div className="flex flex-col items-center justify-center h-full p-8 text-center min-h-[400px]">
      <div className="w-12 h-12 rounded-xl bg-destructive/15 flex items-center justify-center mb-4">
        <AlertTriangle className="w-6 h-6 text-destructive" />
      </div>
      <h2 className="font-mono text-lg font-semibold text-foreground mb-2">
        Something went wrong
      </h2>
      <p className="text-sm text-muted-foreground mb-6 max-w-sm">
        {error.message ?? 'An unexpected error occurred. Please try again.'}
      </p>
      <Button onClick={reset} variant="outline" size="sm" className="gap-2">
        <RefreshCw className="w-3.5 h-3.5" />
        Try again
      </Button>
    </div>
  )
}
