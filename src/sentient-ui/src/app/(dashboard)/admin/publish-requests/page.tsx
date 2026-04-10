'use client'

import { useEffect } from 'react'
import { useRouter } from 'next/navigation'
import { ShieldAlert, BookMarked } from 'lucide-react'
import { useUiStore, selectUser } from '@/store/ui-store'
import { PublishRequestsView } from '@/features/admin/components/PublishRequestsView'
import { Badge } from '@/components/ui/badge'

function AccessDenied() {
  return (
    <div className="flex h-64 flex-col items-center justify-center gap-4 text-center">
      <div className="flex h-14 w-14 items-center justify-center rounded-2xl border border-destructive/30 bg-destructive/10">
        <ShieldAlert className="h-7 w-7 text-destructive" />
      </div>
      <div>
        <p className="font-mono font-semibold text-destructive">Acceso denegado</p>
        <p className="mt-1 text-sm text-muted-foreground">
          Solo los administradores pueden ver esta página
        </p>
      </div>
    </div>
  )
}

export default function PublishRequestsPage() {
  const router = useRouter()
  const user = useUiStore(selectUser)
  const isAdmin = user?.role === 'Admin'

  // If no user at all, redirect to login
  useEffect(() => {
    if (user === null) {
      router.replace('/login')
    }
  }, [user, router])

  if (user === null) return null

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
            Revisá y aprobá los artículos que los usuarios quieren compartir con el equipo.
          </p>
        </div>
      </div>

      {/* Content — guarded */}
      {isAdmin ? <PublishRequestsView /> : <AccessDenied />}
    </div>
  )
}
