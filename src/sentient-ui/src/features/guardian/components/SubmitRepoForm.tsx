'use client'

import { useForm, Controller } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useAction } from 'next-safe-action/hooks'
import { useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import type { z } from 'zod'
import { submitRepoSchema } from '@/lib/schemas'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Loader2, GitFork, Shield } from 'lucide-react'
import { submitRepoAction } from '../actions'
import { REPOSITORY_KEYS } from '../hooks/useRepositories'

type SubmitRepoValues = z.infer<typeof submitRepoSchema>

interface Props {
  onSubmitted?: (repositoryId: string) => void
}

export function SubmitRepoForm({ onSubmitted }: Props = {}) {
  const queryClient = useQueryClient()
  const form = useForm<SubmitRepoValues>({
    resolver: zodResolver(submitRepoSchema),
    defaultValues: {
      repositoryUrl: '',
      trustLevel: 'External',
      notes: '',
    },
  })

  const { execute, isPending } = useAction(submitRepoAction, {
    onSuccess: ({ data }) => {
      toast.success('Repositorio enviado. Análisis en cola.')
      queryClient.invalidateQueries({ queryKey: REPOSITORY_KEYS.all })
      form.reset()
      if (data?.repositoryId) onSubmitted?.(data.repositoryId)
    },
    onError: ({ error }) =>
      toast.error(error.serverError ?? 'Error al enviar el repositorio'),
  })

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center gap-2.5">
          <GitFork className="h-5 w-5 text-primary" />
          <CardTitle className="text-base">Analizar repositorio</CardTitle>
        </div>
        <CardDescription>
          Ingresa una URL de GitHub para análisis estático. Solo se analiza código — nunca se ejecuta.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <form onSubmit={form.handleSubmit((data) => execute(data))} className="space-y-4">
          {/* Repository URL */}
          <div className="space-y-1.5">
            <Label htmlFor="repositoryUrl">URL del repositorio *</Label>
            <Input
              id="repositoryUrl"
              placeholder="https://github.com/org/repo"
              {...form.register('repositoryUrl')}
            />
            {form.formState.errors.repositoryUrl && (
              <p className="text-xs text-destructive">
                {form.formState.errors.repositoryUrl.message}
              </p>
            )}
          </div>

          {/* Trust Level */}
          <div className="space-y-1.5">
            <Label>Nivel de confianza *</Label>
            <Controller
              name="trustLevel"
              control={form.control}
              render={({ field }) => (
                <Select value={field.value} onValueChange={field.onChange}>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="External">
                      <div className="flex items-center gap-2">
                        <Shield className="h-3.5 w-3.5 text-amber-400" />
                        External — análisis de seguridad completo
                      </div>
                    </SelectItem>
                    <SelectItem value="Internal">
                      <div className="flex items-center gap-2">
                        <Shield className="h-3.5 w-3.5 text-emerald-400" />
                        Internal — enfocado en calidad
                      </div>
                    </SelectItem>
                  </SelectContent>
                </Select>
              )}
            />
          </div>

          {/* Notes */}
          <div className="space-y-1.5">
            <Label htmlFor="notes">Notas adicionales</Label>
            <Textarea
              id="notes"
              rows={2}
              placeholder="Contexto adicional sobre el repositorio..."
              {...form.register('notes')}
            />
          </div>

          <Button type="submit" disabled={isPending} className="w-full">
            {isPending ? (
              <>
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                Enviando...
              </>
            ) : (
              <>
                <GitFork className="mr-2 h-4 w-4" />
                Analizar repositorio
              </>
            )}
          </Button>
        </form>
      </CardContent>
    </Card>
  )
}
