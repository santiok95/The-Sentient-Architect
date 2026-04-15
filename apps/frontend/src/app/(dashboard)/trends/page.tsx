import { TrendingUp } from 'lucide-react'
import { TrendsTable } from '@/features/trends/components/TrendsTable'

export default function TrendsPage() {
  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-start justify-between">
        <div>
          <div className="flex items-center gap-2.5">
            <div className="flex h-8 w-8 items-center justify-center rounded-lg border border-primary/30 bg-primary/10">
              <TrendingUp className="h-4 w-4 text-primary" />
            </div>
            <h1 className="font-mono text-xl font-semibold">Trends Radar</h1>
          </div>
          <p className="mt-1.5 text-sm text-muted-foreground max-w-xl">
            Monitor de crecimiento tecnológico. Detecta patrones en repos públicos, blogs y dependencias.
          </p>
        </div>
      </div>

      {/* Table (client component) */}
      <TrendsTable />
    </div>
  )
}
