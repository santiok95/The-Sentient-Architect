'use client'

import { useEffect, useState } from 'react'

/**
 * MSWProvider — initializes the Mock Service Worker in the browser.
 * Only active in development mode when NEXT_PUBLIC_API_MOCK=true.
 */
export function MSWProvider({ children }: { children: React.ReactNode }) {
  const [isReady, setIsReady] = useState(false)

  useEffect(() => {
    const initMocks = async () => {
      if (process.env.NODE_ENV === 'development' && process.env.NEXT_PUBLIC_API_MOCK === 'true') {
        const { worker } = await import('@/mocks/browser')
        await worker.start({
          onUnhandledRequest: 'bypass',
        })
        setIsReady(true)
      } else {
        setIsReady(true)
      }
    }

    initMocks()
  }, [])

  if (!isReady) return null

  return <>{children}</>
}
