'use client'

import { useState } from 'react'
import { Plus, SlidersHorizontal } from 'lucide-react'
import { Button } from '@/components/ui/button'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { SearchBar } from '@/features/brain/components/SearchBar'
import { KnowledgeTable } from '@/features/brain/components/KnowledgeTable'
import { IngestDialog } from '@/features/brain/components/IngestDialog'
import { IngestProgress } from '@/features/brain/components/IngestProgress'

const TYPE_OPTIONS = [
  { label: 'Todos los tipos', value: '' },
  { label: 'Artículo', value: 'Article' },
  { label: 'Nota', value: 'Note' },
  { label: 'Documentación', value: 'Documentation' },
  { label: 'Repositorio', value: 'Repository' },
]

export function KnowledgeTableWrapper() {
  const [searchTerm, setSearchTerm] = useState('')
  const [typeFilter, setTypeFilter] = useState('')
  const [ingestOpen, setIngestOpen] = useState(false)

  return (
    <div className="space-y-4">
      {/* Toolbar */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <SearchBar />
        <div className="flex items-center gap-2 shrink-0">
          <SlidersHorizontal className="h-4 w-4 text-muted-foreground" />
          <Select value={typeFilter} onValueChange={(v) => setTypeFilter(v ?? '')}>
            <SelectTrigger className="w-44 h-9">
              <SelectValue placeholder="Filtrar por tipo" />
            </SelectTrigger>
            <SelectContent>
              {TYPE_OPTIONS.map((opt) => (
                <SelectItem key={opt.value} value={opt.value}>
                  {opt.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Button size="sm" onClick={() => setIngestOpen(true)}>
            <Plus className="mr-1.5 h-4 w-4" />
            Ingestar
          </Button>
        </div>
      </div>

      {/* Real-time ingestion progress */}
      <IngestProgress />

      {/* Table */}
      <KnowledgeTable searchTerm={searchTerm} typeFilter={typeFilter} />

      {/* Dialog */}
      <IngestDialog open={ingestOpen} onOpenChange={setIngestOpen} />
    </div>
  )
}
