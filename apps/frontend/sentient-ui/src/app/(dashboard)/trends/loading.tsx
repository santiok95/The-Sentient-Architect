import { Skeleton } from '@/components/ui/skeleton'

export default function TrendsLoading() {
  return (
    <div className="space-y-6">
      {/* Header skeleton */}
      <div className="flex items-start gap-2.5">
        <Skeleton className="h-8 w-8 rounded-lg" />
        <div>
          <Skeleton className="h-6 w-40" />
          <Skeleton className="mt-1.5 h-4 w-72" />
        </div>
      </div>

      {/* Filter bar skeleton */}
      <div className="flex gap-3">
        <Skeleton className="h-8 w-40 rounded-lg" />
        <Skeleton className="h-8 w-40 rounded-lg" />
        <div className="flex-1" />
        <Skeleton className="h-8 w-32 rounded-lg" />
      </div>

      {/* Stats row skeleton */}
      <div className="flex gap-6">
        <Skeleton className="h-3 w-40" />
        <Skeleton className="h-3 w-24" />
        <Skeleton className="h-3 w-24" />
      </div>

      {/* Table skeleton */}
      <div className="rounded-xl border border-border overflow-hidden">
        <div className="border-b border-border bg-muted/50 px-4 py-2.5">
          <div className="flex gap-8">
            {['Tecnología', 'Categoría', 'Tracción', 'Relevancia', 'Actualizado'].map((h) => (
              <Skeleton key={h} className="h-3 w-20" />
            ))}
          </div>
        </div>
        {Array.from({ length: 8 }).map((_, i) => (
          <div key={i} className="flex items-center gap-8 px-4 py-3.5 border-b border-border/50">
            <div className="space-y-1">
              <Skeleton className="h-4 w-44" />
              <Skeleton className="h-3 w-60" />
            </div>
            <Skeleton className="h-3 w-20" />
            <Skeleton className="h-5 w-24 rounded-full" />
            <Skeleton className="h-1.5 w-32 rounded-full" />
            <Skeleton className="h-3 w-16 ml-auto" />
          </div>
        ))}
      </div>
    </div>
  )
}
