import { Suspense } from 'react'
import { Brain, Sparkles } from 'lucide-react'
import { KnowledgeTableWrapper } from './_components/KnowledgeTableWrapper'
import { Skeleton } from '@/components/ui/skeleton'

export default function BrainPage() {
  return (
    <div className="flex flex-col gap-6">
      {/* Header */}
      <div className="flex items-start justify-between">
        <div>
          <div className="flex items-center gap-2.5 mb-1">
            <Brain className="h-6 w-6 text-primary" />
            <h1 className="text-2xl font-bold tracking-tight">Knowledge Brain</h1>
          </div>
          <p className="text-sm text-muted-foreground">
            Tu base de conocimiento semántica. Ingesta artículos, notas y repositorios.
          </p>
        </div>
        <div className="flex items-center gap-1.5 rounded-full border border-primary/30 bg-primary/10 px-3 py-1">
          <Sparkles className="h-3.5 w-3.5 text-primary" />
          <span className="text-xs font-medium text-primary">RAG activo</span>
        </div>
      </div>

      {/* Controls + Table */}
      <Suspense fallback={<Skeleton className="h-96 w-full rounded-lg" />}>
        <KnowledgeTableWrapper />
      </Suspense>
    </div>
  )
}
