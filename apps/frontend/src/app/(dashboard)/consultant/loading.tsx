import { Skeleton } from '@/components/ui/skeleton'

export default function ConsultantLoading() {
  return (
    <div className="flex h-[calc(100vh-4rem)] -mx-6 -mt-6">
      <div className="w-72 border-r border-border p-3 space-y-2">
        <Skeleton className="h-9 w-full" />
        <div className="h-px bg-border my-2" />
        {Array.from({ length: 5 }).map((_, i) => (
          <Skeleton key={i} className="h-14 w-full rounded-lg" />
        ))}
      </div>
      <div className="flex-1 flex flex-col justify-end p-4 gap-4">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className={`flex gap-3 ${i % 2 === 1 ? 'flex-row-reverse' : ''}`}>
            <Skeleton className="h-7 w-7 rounded-full shrink-0" />
            <Skeleton
              className="h-16 rounded-xl"
              style={{ width: `${40 + (i * 13) % 38}%` }}
            />
          </div>
        ))}
        <Skeleton className="h-16 w-full rounded-lg" />
      </div>
    </div>
  )
}
