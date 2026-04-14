import { Suspense } from 'react'
import { Shield } from 'lucide-react'
import { Skeleton } from '@/components/ui/skeleton'
import { GuardianView } from './_components/GuardianView'

export default function GuardianPage() {
  return (
    <div className="flex flex-col gap-6">
      {/* Header */}
      <div className="flex items-start justify-between">
        <div>
          <div className="flex items-center gap-2.5 mb-1">
            <Shield className="h-6 w-6 text-primary" />
            <h1 className="text-2xl font-bold tracking-tight">Code Guardian</h1>
          </div>
          <p className="text-sm text-muted-foreground">
            Análisis estático de repositorios. Seguridad, calidad y mantenibilidad.
          </p>
        </div>
      </div>

      <Suspense fallback={<Skeleton className="h-96 w-full rounded-lg" />}>
        <GuardianView />
      </Suspense>
    </div>
  )
}