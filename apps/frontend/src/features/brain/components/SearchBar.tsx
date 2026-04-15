'use client'

import { useState, useRef, useEffect } from 'react'
import { Search, Loader2, X, ExternalLink, Tag } from 'lucide-react'
import { Input } from '@/components/ui/input'
import { Badge } from '@/components/ui/badge'
import { ScrollArea } from '@/components/ui/scroll-area'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { useKnowledgeSearch, useKnowledgeTags, type KnowledgeItem } from '../hooks/useKnowledge'

const TYPE_COLORS: Record<string, string> = {
  Article: 'bg-blue-500/20 text-blue-400',
  Note: 'bg-emerald-500/20 text-emerald-400',
  Documentation: 'bg-violet-500/20 text-violet-400',
  Repository: 'bg-amber-500/20 text-amber-400',
}

interface Props {
  onSearch?: (term: string) => void
  activeTag?: string
  onTagChange?: (tag: string) => void
}

export function SearchBar({ onSearch, activeTag = '', onTagChange }: Props) {
  const [query, setQuery] = useState('')
  const [open, setOpen] = useState(false)
  const containerRef = useRef<HTMLDivElement>(null)
  const { mutate, data: results, isPending, reset } = useKnowledgeSearch()
  const { data: tags } = useKnowledgeTags()

  useEffect(() => {
    const handler = setTimeout(() => {
      onSearch?.(query.trim())
      if (query.trim().length >= 3) {
        mutate({ query: query.trim(), maxResults: 8, includeShared: true })
        setOpen(true)
      } else {
        reset()
        setOpen(false)
      }
    }, 400)
    return () => clearTimeout(handler)
  }, [query, mutate, reset, onSearch])

  // Close results on outside click
  useEffect(() => {
    function handleClick(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setOpen(false)
      }
    }
    document.addEventListener('mousedown', handleClick)
    return () => document.removeEventListener('mousedown', handleClick)
  }, [])

  function clear() {
    setQuery('')
    reset()
    setOpen(false)
    onSearch?.('')
  }

  function selectTag(tag: string) {
    onTagChange?.(tag)
    // Clear text search when filtering by tag
    clear()
  }

  function clearTag() {
    onTagChange?.('')
  }

  return (
    <div className="flex items-center gap-2 w-full max-w-xl">
      {/* Semantic search input */}
      <div ref={containerRef} className="relative flex-1">
        <div className="relative">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Buscar en el conocimiento semántico..."
            className="pl-9 pr-9"
            onFocus={() => results && results.length > 0 && setOpen(true)}
          />
          {(query || isPending) && (
            <button
              onClick={clear}
              className="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground transition-colors"
            >
              {isPending ? (
                <Loader2 className="h-4 w-4 animate-spin" />
              ) : (
                <X className="h-4 w-4" />
              )}
            </button>
          )}
        </div>

        {/* Results dropdown */}
        {open && results && (
          <div className="absolute left-0 right-0 top-[calc(100%+4px)] z-50 rounded-lg border border-border bg-card shadow-xl">
            {results.length === 0 ? (
              <p className="py-6 text-center text-sm text-muted-foreground">
                Sin resultados para &ldquo;{query}&rdquo;
              </p>
            ) : (
              <ScrollArea className="max-h-72">
                <ul className="py-1">
                  {(results as KnowledgeItem[]).map((item) => (
                    <li key={item.id}>
                      <div className="flex items-start gap-3 px-3 py-2.5 hover:bg-muted/50 cursor-pointer transition-colors">
                        <div className="flex-1 min-w-0">
                          <p className="truncate text-sm font-mono text-foreground/90">
                            {item.title}
                          </p>
                          <div className="flex items-center gap-2 mt-0.5">
                            <Badge
                              variant="outline"
                              className={`text-xs h-4 px-1 ${TYPE_COLORS[item.type] ?? ''}`}
                            >
                              {item.type}
                            </Badge>
                            {item.tags.slice(0, 2).map((t) => (
                              <button
                                key={t}
                                className="text-xs text-muted-foreground hover:text-primary transition-colors"
                                onClick={(e) => { e.stopPropagation(); selectTag(t) }}
                              >
                                #{t}
                              </button>
                            ))}
                          </div>
                        </div>
                        {item.sourceUrl && (
                          <a
                            href={item.sourceUrl}
                            target="_blank"
                            rel="noopener noreferrer"
                            onClick={(e) => e.stopPropagation()}
                            className="shrink-0 text-muted-foreground hover:text-foreground"
                          >
                            <ExternalLink className="h-3.5 w-3.5" />
                          </a>
                        )}
                      </div>
                    </li>
                  ))}
                </ul>
              </ScrollArea>
            )}
            <div className="border-t border-border px-3 py-2">
              <p className="text-xs text-muted-foreground">
                {results.length} resultado{results.length !== 1 ? 's' : ''} — búsqueda semántica
              </p>
            </div>
          </div>
        )}
      </div>

      {/* Tag filter — active pill or picker button */}
      {activeTag ? (
        <Badge
          variant="secondary"
          className="gap-1 pl-2 pr-1 py-1 h-9 shrink-0 cursor-default font-normal"
        >
          <Tag className="h-3 w-3" />
          #{activeTag}
          <button
            onClick={clearTag}
            className="ml-0.5 hover:text-destructive transition-colors"
            aria-label="Quitar filtro de tag"
          >
            <X className="h-3 w-3" />
          </button>
        </Badge>
      ) : (
        <DropdownMenu>
          <DropdownMenuTrigger
            className="inline-flex items-center justify-center h-9 w-9 shrink-0 rounded-md border border-input bg-background text-muted-foreground hover:bg-accent hover:text-accent-foreground transition-colors"
            aria-label="Filtrar por tag"
          >
            <Tag className="h-4 w-4" />
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-52">
            {!tags || tags.length === 0 ? (
              <div className="py-3 text-center text-xs text-muted-foreground">
                No hay tags disponibles
              </div>
            ) : (
              <ScrollArea className="max-h-56">
                {tags.map((t) => (
                  <DropdownMenuItem
                    key={t.name}
                    onClick={() => selectTag(t.name)}
                    className="flex items-center justify-between cursor-pointer"
                  >
                    <span>#{t.name}</span>
                    <span className="text-xs text-muted-foreground">{t.count}</span>
                  </DropdownMenuItem>
                ))}
              </ScrollArea>
            )}
          </DropdownMenuContent>
        </DropdownMenu>
      )}
    </div>
  )
}
