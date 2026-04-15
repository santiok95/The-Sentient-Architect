'use client'

import { useState } from 'react'
import { useForm, Controller } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useAction } from 'next-safe-action/hooks'
import { toast } from 'sonner'
import { useQueryClient } from '@tanstack/react-query'
import type { z } from 'zod'
import { ingestKnowledgeSchema } from '@/lib/schemas'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { Label } from '@/components/ui/label'
import { Badge } from '@/components/ui/badge'
import { Loader2, X } from 'lucide-react'
import { ingestKnowledgeAction } from '../actions'
import { KNOWLEDGE_KEYS } from '../hooks/useKnowledge'

type IngestFormValues = z.infer<typeof ingestKnowledgeSchema>

interface Props {
  open: boolean
  onOpenChange: (open: boolean) => void
}

const KNOWLEDGE_TYPES = ['Article', 'Note', 'Documentation', 'Repository'] as const

export function IngestDialog({ open, onOpenChange }: Props) {
  const [tagInput, setTagInput] = useState('')
  const queryClient = useQueryClient()

  const form = useForm<IngestFormValues>({
    resolver: zodResolver(ingestKnowledgeSchema),
    defaultValues: {
      title: '',
      content: '',
      sourceUrl: '',
      type: 'Article',
      tags: [],
    },
  })

  const tags = form.watch('tags')

  const { execute, isPending } = useAction(ingestKnowledgeAction, {
    onSuccess: ({ data }) => {
      toast.success(`"${data?.title}" ingested correctamente`)
      queryClient.invalidateQueries({ queryKey: KNOWLEDGE_KEYS.all })
      onOpenChange(false)
      form.reset()
      setTagInput('')
    },
    onError: ({ error }) =>
      toast.error(error.serverError ?? 'Error al ingestar el conocimiento'),
  })

  function addTag() {
    const trimmed = tagInput.trim().toLowerCase()
    if (!trimmed || tags.includes(trimmed) || tags.length >= 10) return
    form.setValue('tags', [...tags, trimmed])
    setTagInput('')
  }

  function removeTag(tag: string) {
    form.setValue('tags', tags.filter((t) => t !== tag))
  }

  function handleOpenChange(next: boolean) {
    if (!next) {
      form.reset()
      setTagInput('')
    }
    onOpenChange(next)
  }

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="sm:max-w-xl flex flex-col max-h-[90vh]">
        <DialogHeader>
          <DialogTitle>Ingestar conocimiento</DialogTitle>
          <DialogDescription>
            Agrega un nuevo artículo, nota, documentación o repositorio a tu base de conocimiento.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={form.handleSubmit((data) => execute(data))} className="flex flex-col flex-1 min-h-0">
        <div className="flex-1 overflow-y-auto space-y-4 pr-1">
          {/* Title */}
          <div className="space-y-1.5">
            <Label htmlFor="title">Título *</Label>
            <Input
              id="title"
              placeholder="Ej: CQRS en .NET 9 con Vertical Slice"
              {...form.register('title')}
            />
            {form.formState.errors.title && (
              <p className="text-xs text-destructive">{form.formState.errors.title.message}</p>
            )}
          </div>

          {/* Type */}
          <div className="space-y-1.5">
            <Label>Tipo *</Label>
            <Controller
              name="type"
              control={form.control}
              render={({ field }) => (
                <Select value={field.value} onValueChange={field.onChange}>
                  <SelectTrigger>
                    <SelectValue placeholder="Selecciona el tipo" />
                  </SelectTrigger>
                  <SelectContent>
                    {KNOWLEDGE_TYPES.map((t) => (
                      <SelectItem key={t} value={t}>{t}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            />
          </div>

          {/* Source URL */}
          <div className="space-y-1.5">
            <Label htmlFor="sourceUrl">URL de fuente</Label>
            <Input
              id="sourceUrl"
              type="url"
              placeholder="https://..."
              {...form.register('sourceUrl')}
            />
            {form.formState.errors.sourceUrl && (
              <p className="text-xs text-destructive">{form.formState.errors.sourceUrl.message}</p>
            )}
          </div>

          {/* Content */}
          <div className="space-y-1.5">
            <Label htmlFor="content">Contenido</Label>
            <Textarea
              id="content"
              rows={5}
              placeholder="Pega el contenido del artículo o notas aquí..."
              className="resize-y"
              {...form.register('content')}
            />
          </div>

          {/* Tags */}
          <div className="space-y-1.5">
            <Label>Tags</Label>
            <div className="flex gap-2">
              <Input
                placeholder="Agregar tag y presionar Enter"
                value={tagInput}
                onChange={(e) => setTagInput(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') {
                    e.preventDefault()
                    addTag()
                  }
                }}
              />
              <Button type="button" variant="outline" onClick={addTag} disabled={!tagInput.trim()}>
                Agregar
              </Button>
            </div>
            {tags.length > 0 && (
              <div className="flex flex-wrap gap-1 mt-2">
                {tags.map((tag) => (
                  <Badge key={tag} variant="secondary" className="gap-1 pl-2 pr-1 py-0.5">
                    {tag}
                    <button type="button" onClick={() => removeTag(tag)} className="hover:text-destructive">
                      <X className="h-3 w-3" />
                    </button>
                  </Badge>
                ))}
              </div>
            )}
          </div>

        </div>

          <DialogFooter className="pt-4">
            <Button
              type="button"
              variant="outline"
              onClick={() => handleOpenChange(false)}
              disabled={isPending}
            >
              Cancelar
            </Button>
            <Button type="submit" disabled={isPending}>
              {isPending && <Loader2 className="mr-2 h-3.5 w-3.5 animate-spin" />}
              Ingestar
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
