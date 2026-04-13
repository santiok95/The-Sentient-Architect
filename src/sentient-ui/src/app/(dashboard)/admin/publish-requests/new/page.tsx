'use client'

import { useEffect } from 'react'
import { useRouter } from 'next/navigation'
import { BookMarked } from 'lucide-react'
import { useUiStore, selectUser, selectHasHydrated } from '@/store/ui-store'
import { NewPublishRequestForm } from '@/features/admin/components/NewPublishRequestForm'

export default function NewPublishRequestPage() {
  const router = useRouter()
  const user = useUiStore(selectUser)
  const hasHydrated = useUiStore(selectHasHydrated)

  useEffect(() => {
    if (!hasHydrated) return
    if (user === null) router.replace('/login')
  }, [hasHydrated, user, router])

  if (!hasHydrated || user === null) return null

  return (
    <div className="space-y-6 max-w-2xl">
      {/* Header */}
      <div>
        <div className="flex items-center gap-2.5">
          <div className="flex h-8 w-8 items-center justify-center rounded-lg border border-primary/30 bg-primary/10">
            <BookMarked className="h-4 w-4 text-primary" />
          </div>
          <h1 className="font-mono text-xl font-semibold">Nueva Solicitud de Publicación</h1>
        </div>
        <p className="mt-1.5 text-sm text-muted-foreground max-w-xl">
          {user.role === 'Admin'
            ? 'Como administrador, tu solicitud se aprobará automáticamente.'
            : 'Seleccioná un artículo de tu base de conocimiento y envialo para revisión.'}
        </p>
      </div>

      <NewPublishRequestForm user={user} onSuccess={() => router.push('/admin/publish-requests')} />
    </div>
  )
}
