import { Skeleton } from '@/components/ui/skeleton'

export default function PublishRequestsLoading() {
  return (
    <div className="space-y-6">
      {/* Header skeleton */}
      <div className="flex items-start gap-2.5">
        <Skeleton className="h-8 w-8 rounded-lg" />
        <div>
          <Skeleton className="h-6 w-56" />
          <Skeleton className="mt-1.5 h-4 w-80" />
        </div>
      </div>

      {/* Filter tabs skeleton */}
      <div className="flex gap-2">
        {[80, 70, 75, 60].map((w, i) => (
          <Skeleton key={i} className={`h-8 w-${w > 70 ? '24' : '20'} rounded-lg`} />
        ))}
      </div>

      {/* Table skeleton */}
      <div className="rounded-xl border border-border overflow-hidden">
        <div className="border-b border-border bg-muted/50 px-4 py-2.5">
          <div className="flex gap-8">
            {['Artículo', 'Solicitado por', 'Fecha', 'Estado', 'Acciones'].map((h) => (
              <Skeleton key={h} className="h-3 w-20" />
            ))}
          </div>
        </div>
        {Array.from({ length: 5 }).map((_, i) => (
          <div key={i} className="flex items-center gap-6 px-4 py-3.5 border-b border-border/50">
            <div className="flex-1 space-y-1">
              <Skeleton className="h-4 w-52" />
              <Skeleton className="h-3 w-64" />
            </div>
            <Skeleton className="h-4 w-32" />
            <Skeleton className="h-3 w-20" />
            <Skeleton className="h-5 w-24 rounded-full" />
            <Skeleton className="h-7 w-32 rounded-md ml-auto" />
          </div>
        ))}
      </div>
    </div>
  )
}
