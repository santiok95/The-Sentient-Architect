import { Skeleton } from '@/components/ui/skeleton'

/**
 * Dashboard loading skeleton — mirrors the exact structure of page.tsx
 * so there's no layout shift when data arrives.
 */
export default function DashboardLoading() {
  return (
    <div className="p-7 pb-10">
      {/* Header */}
      <div className="mb-6">
        <Skeleton className="h-8 w-72 mb-2" />
        <Skeleton className="h-4 w-96" />
      </div>

      {/* Quick actions */}
      <div className="flex gap-2.5 mb-7 flex-wrap">
        <Skeleton className="h-9 w-36 rounded-lg" />
        <Skeleton className="h-9 w-40 rounded-lg" />
        <Skeleton className="h-9 w-32 rounded-lg" />
      </div>

      {/* Stats grid */}
      <div className="grid grid-cols-2 xl:grid-cols-4 gap-3.5 mb-7">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="bg-card border border-border rounded-xl p-[18px_20px]">
            <div className="flex items-center justify-between mb-3">
              <Skeleton className="h-3 w-28" />
              <Skeleton className="h-[30px] w-[30px] rounded-lg" />
            </div>
            <Skeleton className="h-8 w-16 mb-1" />
            <Skeleton className="h-3 w-24" />
          </div>
        ))}
      </div>

      {/* Main grid */}
      <div className="grid grid-cols-1 xl:grid-cols-[1fr_380px] gap-4">
        {/* Table skeleton */}
        <div className="bg-card border border-border rounded-xl overflow-hidden">
          <div className="px-5 py-4 border-b border-border">
            <Skeleton className="h-4 w-52" />
          </div>
          <div>
            {Array.from({ length: 5 }).map((_, i) => (
              <div key={i} className="flex items-center gap-4 px-5 py-3 border-b border-border/50 last:border-b-0">
                <div className="flex-1">
                  <Skeleton className="h-3.5 w-56 mb-1.5" />
                  <Skeleton className="h-3 w-28" />
                </div>
                <Skeleton className="h-5 w-16 rounded-full" />
                <Skeleton className="h-5 w-16 rounded-full" />
                <Skeleton className="h-2.5 w-2.5 rounded-full" />
              </div>
            ))}
          </div>
        </div>

        {/* Right column skeleton */}
        <div className="flex flex-col gap-4">
          {/* Trends skeleton */}
          <div className="bg-card border border-border rounded-xl overflow-hidden">
            <div className="px-5 py-4 border-b border-border">
              <Skeleton className="h-4 w-36" />
            </div>
            {Array.from({ length: 4 }).map((_, i) => (
              <div key={i} className="flex items-center gap-3 px-5 py-3 border-b border-border/50 last:border-b-0">
                <Skeleton className="h-[34px] w-[34px] rounded-lg flex-shrink-0" />
                <div className="flex-1">
                  <Skeleton className="h-3 w-28 mb-1.5" />
                  <Skeleton className="h-2.5 w-20" />
                </div>
                <Skeleton className="h-5 w-16 rounded-full" />
              </div>
            ))}
          </div>

          {/* Approvals skeleton */}
          <div className="bg-card border border-border rounded-xl overflow-hidden">
            <div className="px-5 py-4 border-b border-border">
              <Skeleton className="h-4 w-36" />
            </div>
            {Array.from({ length: 3 }).map((_, i) => (
              <div key={i} className="flex items-start gap-3 px-5 py-3.5 border-b border-border/50 last:border-b-0">
                <Skeleton className="h-7 w-7 rounded-full flex-shrink-0" />
                <div className="flex-1">
                  <Skeleton className="h-3 w-48 mb-1.5" />
                  <Skeleton className="h-2.5 w-32" />
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  )
}
