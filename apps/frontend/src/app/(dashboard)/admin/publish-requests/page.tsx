'use client'

import { useEffect } from 'react'
import { useRouter } from 'next/navigation'
import { BookMarked, Plus } from 'lucide-react'
import { useUiStore, selectUser, selectHasHydrated } from '@/store/ui-store'
import { PublishRequestsView } from '@/features/admin/components/PublishRequestsView'
import { MyPublishRequestsView } from '@/features/brain/components/MyPublishRequestsView'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'

export default function PublishRequestsPage() {
  const router = useRouter()
  const user = useUiStore(selectUser)
  const hasHydrated = useUiStore(selectHasHydrated)
  const isAdmin = user?.role === 'Admin'

  useEffect(() => {
    if (!hasHydrated) return
    if (user === null) router.replace('/login')
  }, [hasHydrated, user, router])

  if (!hasHydrated || user === null) return null

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-start justify-between">
        <div>
          <div className="flex items-center gap-2.5">
            <div className="flex h-8 w-8 items-center justify-center rounded-lg border border-primary/30 bg-primary/10">
              <BookMarked className="h-4 w-4 text-primary" />
            </div>
            <h1 className="font-mono text-xl font-semibold">Solicitudes de Publicación</h1>
            {isAdmin && (
              <Badge variant="outline" className="font-mono text-xs border-primary/40 text-primary">
                Admin
              </Badge>
            )}
          </div>
          <p className="mt-1.5 text-sm text-muted-foreground max-w-xl">
            {isAdmin
              ? 'Revisá y aprobá los artículos que los usuarios quieren compartir con el equipo.'
              : 'Seguí el estado de tus solicitudes de publicación.'}
          </p>
        </div>
        {isAdmin && (
          <Button
            size="sm"
            className="h-8 gap-1.5 text-xs font-mono"
            onClick={() => router.push('/admin/publish-requests/new')}
          >
            <Plus className="h-3.5 w-3.5" />
            Nueva solicitud
          </Button>
        )}
      </div>

      {/* Content — admin sees all requests with review actions, users see their own */}
      {isAdmin ? <PublishRequestsView /> : <MyPublishRequestsView />}
    </div>
  )
}
