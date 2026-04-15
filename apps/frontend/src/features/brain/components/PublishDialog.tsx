'use client'

import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useAction } from 'next-safe-action/hooks'
import { toast } from 'sonner'
import type { z } from 'zod'
import { publishRequestSchema } from '@/lib/schemas'
import { publishKnowledgeAction } from '../actions'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/textarea'
import { Label } from '@/components/ui/label'
import { Loader2 } from 'lucide-react'

type PublishFormValues = z.infer<typeof publishRequestSchema>

interface Props {
  open: boolean
  onOpenChange: (open: boolean) => void
  knowledgeItemId: string
  title: string
  onSuccess?: () => void
}

export function PublishDialog({
  open,
  onOpenChange,
  knowledgeItemId,
  title,
  onSuccess,
}: Props) {
  const form = useForm<PublishFormValues>({
    resolver: zodResolver(publishRequestSchema),
    defaultValues: { knowledgeItemId, reason: '' },
  })

  const { execute, isPending } = useAction(publishKnowledgeAction, {
    onSuccess: () => {
      toast.success('Solicitud de publicación enviada')
      onOpenChange(false)
      form.reset()
      onSuccess?.()
    },
    onError: ({ error }) => toast.error(error.serverError ?? 'Error al enviar solicitud'),
  })

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Solicitar publicación compartida</DialogTitle>
          <DialogDescription className="line-clamp-2">
            &ldquo;{title}&rdquo;
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={form.handleSubmit((data) => execute(data))} className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="reason">Motivo de la publicación</Label>
            <Textarea
              id="reason"
              rows={4}
              placeholder="Explica por qué este contenido debería ser compartido con el equipo..."
              {...form.register('reason')}
            />
            {form.formState.errors.reason && (
              <p className="text-xs text-destructive">{form.formState.errors.reason.message}</p>
            )}
          </div>

          <DialogFooter>
            <Button
              type="button"
              variant="outline"
              onClick={() => onOpenChange(false)}
              disabled={isPending}
            >
              Cancelar
            </Button>
            <Button type="submit" disabled={isPending}>
              {isPending && <Loader2 className="mr-2 h-3.5 w-3.5 animate-spin" />}
              Enviar solicitud
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
