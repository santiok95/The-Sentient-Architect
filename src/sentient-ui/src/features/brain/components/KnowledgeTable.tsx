'use client'

import { useState } from 'react'
import { useAction } from 'next-safe-action/hooks'
import { toast } from 'sonner'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { Badge } from '@/components/ui/badge'
import { Button, buttonVariants } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import { Skeleton } from '@/components/ui/skeleton'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { MoreHorizontal, Trash2, Globe, Loader2, ExternalLink } from 'lucide-react'
import { useKnowledgeItems, type KnowledgeItem, useInvalidateKnowledge } from '../hooks/useKnowledge'
import { deleteKnowledgeAction } from '../actions'
import { PublishDialog } from './PublishDialog'

const TYPE_COLORS: Record<string, string> = {
  Article: 'bg-blue-500/20 text-blue-400 border-blue-500/30',
  Note: 'bg-emerald-500/20 text-emerald-400 border-emerald-500/30',
  Documentation: 'bg-violet-500/20 text-violet-400 border-violet-500/30',
  Repository: 'bg-amber-500/20 text-amber-400 border-amber-500/30',
}

const STATUS_DOT: Record<string, string> = {
  Completed: 'bg-emerald-400',
  Processing: 'bg-amber-400 animate-pulse',
  Pending: 'bg-sky-400 animate-pulse',
  Failed: 'bg-red-400',
}

interface Props {
  searchTerm?: string
  typeFilter?: string
}

function KnowledgeRowActions({ item }: { item: KnowledgeItem }) {
  const [publishOpen, setPublishOpen] = useState(false)
  const invalidate = useInvalidateKnowledge()

  const { execute: executeDelete, isPending: isDeleting } = useAction(deleteKnowledgeAction, {
    onSuccess: () => {
      toast.success('Elemento eliminado')
      invalidate()
    },
    onError: ({ error }) => toast.error(error.serverError ?? 'Error al eliminar'),
  })

  return (
    <>
      <DropdownMenu>
        <DropdownMenuTrigger
          className={cn(buttonVariants({ variant: 'ghost', size: 'icon' }), 'h-7 w-7')}
        >
          {isDeleting ? (
            <Loader2 className="h-3.5 w-3.5 animate-spin" />
          ) : (
            <MoreHorizontal className="h-3.5 w-3.5" />
          )}
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="w-44">
          {item.sourceUrl && (
            <DropdownMenuItem
              onClick={() => window.open(item.sourceUrl, '_blank', 'noopener,noreferrer')}
            >
              <ExternalLink className="mr-2 h-3.5 w-3.5" />
              Ver fuente
            </DropdownMenuItem>
          )}
          {item.scope === 'Personal' && item.processingStatus === 'Completed' && (
            <DropdownMenuItem onClick={() => setPublishOpen(true)}>
              <Globe className="mr-2 h-3.5 w-3.5" />
              Solicitar publicación
            </DropdownMenuItem>
          )}
          <DropdownMenuSeparator />
          <DropdownMenuItem
            className="text-destructive focus:text-destructive"
            onClick={() => executeDelete({ id: item.id })}
            disabled={isDeleting}
          >
            <Trash2 className="mr-2 h-3.5 w-3.5" />
            Eliminar
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>

      <PublishDialog
        open={publishOpen}
        onOpenChange={setPublishOpen}
        knowledgeItemId={item.id}
        title={item.title}

        onSuccess={invalidate}
      />
    </>
  )
}

export function KnowledgeTable({ searchTerm = '', typeFilter = '' }: Props) {
  const [page, setPage] = useState(1)
  const PAGE_SIZE = 20
  const { data, isLoading, isError } = useKnowledgeItems(page, PAGE_SIZE, searchTerm, typeFilter)

  if (isError) {
    return (
      <div className="flex h-32 items-center justify-center text-sm text-destructive">
        Error al cargar el conocimiento. Verifica la conexión con el servidor.
      </div>
    )
  }

  return (
    <div className="space-y-2">
      <div className="rounded-lg border border-border overflow-hidden">
        <Table>
          <TableHeader>
            <TableRow className="hover:bg-transparent border-border">
              <TableHead className="w-[40%]">Título</TableHead>
              <TableHead>Tipo</TableHead>
              <TableHead>Ámbito</TableHead>
              <TableHead>Estado</TableHead>
              <TableHead>Tags</TableHead>
              <TableHead className="text-right w-12">Acciones</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading
              ? Array.from({ length: 6 }).map((_, i) => (
                  <TableRow key={i}>
                    <TableCell><Skeleton className="h-4 w-4/5" /></TableCell>
                    <TableCell><Skeleton className="h-5 w-20" /></TableCell>
                    <TableCell><Skeleton className="h-5 w-16" /></TableCell>
                    <TableCell><Skeleton className="h-5 w-20" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-24" /></TableCell>
                    <TableCell><Skeleton className="h-7 w-7 ml-auto" /></TableCell>
                  </TableRow>
                ))
              : data?.items.map((item) => (
                  <TableRow key={item.id} className="group">
                    <TableCell>
                      <span className="font-mono text-sm text-foreground/90 group-hover:text-foreground transition-colors">
                        {item.title}
                      </span>
                      {item.sourceUrl && (
                        <p className="text-xs text-muted-foreground truncate max-w-xs mt-0.5">
                          {item.sourceUrl}
                        </p>
                      )}
                    </TableCell>
                    <TableCell>
                      <Badge
                        variant="outline"
                        className={`text-xs font-mono ${TYPE_COLORS[item.type] ?? ''}`}
                      >
                        {item.type}
                      </Badge>
                    </TableCell>
                    <TableCell>
                      <Badge variant="secondary" className="text-xs">
                        {item.scope === 'Personal' ? 'Personal' : 'Compartido'}
                      </Badge>
                    </TableCell>
                    <TableCell>
                      <div className="flex items-center gap-2">
                        <span
                          className={`inline-block h-2 w-2 rounded-full ${STATUS_DOT[item.processingStatus] ?? 'bg-muted'}`}
                        />
                        <span className="text-xs text-muted-foreground">{item.processingStatus}</span>
                      </div>
                    </TableCell>
                    <TableCell>
                      <div className="flex gap-1 flex-wrap max-w-[160px]">
                        {item.tags.slice(0, 3).map((tag) => (
                          <Badge key={tag} variant="outline" className="text-xs px-1.5 py-0 h-5">
                            {tag}
                          </Badge>
                        ))}
                        {item.tags.length > 3 && (
                          <span className="text-xs text-muted-foreground">+{item.tags.length - 3}</span>
                        )}
                      </div>
                    </TableCell>
                    <TableCell className="text-right">
                      <KnowledgeRowActions item={item} />
                    </TableCell>
                  </TableRow>
                ))}

            {!isLoading && data?.items.length === 0 && (
              <TableRow>
                <TableCell colSpan={6} className="text-center text-muted-foreground py-12">
                  No hay elementos. Ingesta tu primer artículo o nota.
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </div>

      {/* Pagination */}
      {data && data.totalCount > PAGE_SIZE && (
        <div className="flex items-center justify-between px-1">
          <p className="text-xs text-muted-foreground">
            {(page - 1) * PAGE_SIZE + 1}–{Math.min(page * PAGE_SIZE, data.totalCount)} de {data.totalCount}
          </p>
          <div className="flex gap-2">
            <Button
              variant="outline"
              size="sm"
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              disabled={page === 1}
            >
              Anterior
            </Button>
            <Button
              variant="outline"
              size="sm"
              onClick={() => setPage((p) => p + 1)}
              disabled={page * PAGE_SIZE >= data.totalCount}
            >
              Siguiente
            </Button>
          </div>
        </div>
      )}
    </div>
  )
}
