import { Skeleton } from '@/components/ui/skeleton'

export default function BrainLoading() {
  return (
    <div className="flex flex-col gap-6">
      {/* Header skeleton */}
      <div className="flex items-start justify-between">
        <div className="space-y-2">
          <Skeleton className="h-8 w-52" />
          <Skeleton className="h-4 w-80" />
        </div>
        <Skeleton className="h-7 w-28 rounded-full" />
      </div>

      {/* Toolbar skeleton */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <Skeleton className="h-9 w-full max-w-xl" />
        <div className="flex items-center gap-2">
          <Skeleton className="h-9 w-44" />
          <Skeleton className="h-9 w-24" />
        </div>
      </div>

      {/* Table skeleton */}
      <div className="rounded-lg border border-border overflow-hidden">
        <div className="border-b border-border bg-muted/30 px-4 py-3 flex gap-4">
          {[40, 10, 10, 10, 15, 5].map((w, i) => (
            <Skeleton key={i} className={`h-4 w-${w === 40 ? '2/5' : w + '%'}`} style={{ width: `${w}%` }} />
          ))}
        </div>
        {Array.from({ length: 8 }).map((_, i) => (
          <div key={i} className="flex gap-4 border-b border-border px-4 py-3 last:border-0">
            <div className="w-2/5 space-y-1.5">
              <Skeleton className="h-4 w-4/5" />
              <Skeleton className="h-3 w-2/5" />
            </div>
            <Skeleton className="h-5 w-[10%]" />
            <Skeleton className="h-5 w-[10%]" />
            <div className="flex items-center gap-1.5 w-[10%]">
              <Skeleton className="h-2 w-2 rounded-full" />
              <Skeleton className="h-3.5 w-14" />
            </div>
            <div className="flex gap-1 w-[15%]">
              <Skeleton className="h-4 w-14" />
              <Skeleton className="h-4 w-10" />
            </div>
            <Skeleton className="h-7 w-7 ml-auto" />
          </div>
        ))}
      </div>
    </div>
  )
}
