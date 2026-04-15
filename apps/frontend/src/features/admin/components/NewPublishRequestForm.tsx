'use client'

import { useState } from 'react'
import { useAction } from 'next-safe-action/hooks'
import { toast } from 'sonner'
import { CheckCircle2, Loader2, ShieldCheck, Clock } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/textarea'
import { Label } from '@/components/ui/label'
import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { cn } from '@/lib/utils'
import { useKnowledgeItems } from '@/features/brain/hooks/useKnowledge'
import { publishKnowledgeAction } from '@/features/brain/actions'
import type { AuthUser } from '@/lib/auth'

const TYPE_LABELS: Record<string, string> = {
  Article:       'Artículo',
  Note:          'Nota',
  Documentation: 'Doc',
  Repository:    'Repo',
}

interface Props {
  user: AuthUser
  onSuccess: () => void
}

export function NewPublishRequestForm({ user, onSuccess }: Props) {
  const [selectedId, setSelectedId] = useState<string>('')
  const [reason, setReason] = useState('')
  const [submitted, setSubmitted] = useState(false)
  const [submittedAsAdmin, setSubmittedAsAdmin] = useState(false)

  const isAdmin = user.role === 'Admin'

  const { data, isLoading } = useKnowledgeItems(1, 100)
  // Only show Personal + Completed items — those are candidates for publishing
  const candidates = (data?.items ?? []).filter(
    (i) => i.processingStatus === 'Completed',
  )

  const selectedItem = candidates.find((i) => i.id === selectedId)
  const canSubmit = !!selectedId && (isAdmin || reason.trim().length >= 10)

  const { executeAsync, isPending } = useAction(publishKnowledgeAction)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!canSubmit) return

    const result = await executeAsync({
      knowledgeItemId: selectedId,
      reason: reason.trim(),
    })

    if (result?.serverError) {
      toast.error(result.serverError, { style: { fontFamily: 'var(--font-fira-code)' } })
      return
    }

    setSubmitted(true)
    setSubmittedAsAdmin(isAdmin)
  }

  if (submitted) {
    return (
      <div className="rounded-xl border border-border p-8 flex flex-col items-center gap-4 text-center">
        {submittedAsAdmin ? (
          <>
            <div className="flex h-14 w-14 items-center justify-center rounded-2xl border border-emerald-500/30 bg-emerald-500/10">
              <ShieldCheck className="h-7 w-7 text-emerald-400" />
            </div>
            <div>
              <p className="font-mono font-semibold text-emerald-400">Publicado automáticamente</p>
              <p className="mt-1 text-sm text-muted-foreground max-w-xs">
                Como administrador, el contenido ya está disponible para todos.
              </p>
            </div>
          </>
        ) : (
          <>
            <div className="flex h-14 w-14 items-center justify-center rounded-2xl border border-primary/30 bg-primary/10">
              <Clock className="h-7 w-7 text-primary" />
            </div>
            <div>
              <p className="font-mono font-semibold">Solicitud enviada</p>
              <p className="mt-1 text-sm text-muted-foreground max-w-xs">
                Un administrador revisará tu solicitud y la aprobará o rechazará.
              </p>
            </div>
          </>
        )}
        <div className="flex gap-2 mt-2">
          <Button variant="outline" size="sm" className="font-mono text-xs" onClick={() => {
            setSubmitted(false)
            setSelectedId('')
            setReason('')
          }}>
            Nueva solicitud
          </Button>
          <Button size="sm" className="font-mono text-xs" onClick={onSuccess}>
            Ver solicitudes
          </Button>
        </div>
      </div>
    )
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-5">
      {/* Admin auto-approve banner */}
      {isAdmin && (
        <div className="flex items-center gap-2.5 rounded-lg border border-emerald-500/30 bg-emerald-500/10 px-4 py-3">
          <ShieldCheck className="h-4 w-4 text-emerald-400 shrink-0" />
          <p className="text-sm text-emerald-400 font-mono">
            Tu solicitud se aprobará automáticamente
          </p>
        </div>
      )}

      {/* Knowledge item selector */}
      <div className="space-y-2">
        <Label className="font-mono text-xs text-muted-foreground uppercase tracking-wide">
          Artículo a publicar <span className="text-destructive">*</span>
        </Label>
        {isLoading ? (
          <Skeleton className="h-9 w-full rounded-md" />
        ) : candidates.length === 0 ? (
          <p className="text-sm text-muted-foreground border border-border rounded-md px-3 py-2">
            No tenés artículos procesados disponibles para publicar.
          </p>
        ) : (
          <Select value={selectedId} onValueChange={(v) => setSelectedId(v ?? '')}>
            <SelectTrigger className="w-full font-mono text-sm h-9">
              <SelectValue placeholder="Seleccioná un artículo..." />
            </SelectTrigger>
            <SelectContent>
              {candidates.map((item) => (
                <SelectItem key={item.id} value={item.id} className="font-mono text-xs">
                  <span className="flex items-center gap-2">
                    <span className="text-[10px] px-1.5 py-0.5 rounded bg-muted text-muted-foreground shrink-0">
                      {TYPE_LABELS[item.type] ?? item.type}
                    </span>
                    <span className="truncate">{item.title}</span>
                  </span>
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        )}

        {/* Selected item preview */}
        {selectedItem && (
          <div className="rounded-md border border-border bg-muted/30 px-3 py-2.5 space-y-1">
            <div className="flex items-center gap-2">
              <Badge variant="outline" className="text-[10px] font-mono px-1.5 py-0">
                {TYPE_LABELS[selectedItem.type] ?? selectedItem.type}
              </Badge>
              <span className="text-xs font-medium text-foreground truncate">
                {selectedItem.title}
              </span>
            </div>
            {selectedItem.sourceUrl && (
              <p className="text-[11px] text-muted-foreground truncate font-mono">
                {selectedItem.sourceUrl}
              </p>
            )}
          </div>
        )}
      </div>

      {/* Reason */}
      <div className="space-y-2">
        <Label htmlFor="reason" className="font-mono text-xs text-muted-foreground uppercase tracking-wide">
          Motivo {!isAdmin && <span className="text-destructive">*</span>}
          {isAdmin && <span className="text-muted-foreground/60 normal-case tracking-normal ml-1">(opcional)</span>}
        </Label>
        <Textarea
          id="reason"
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          placeholder={isAdmin
            ? 'Contexto adicional (opcional)...'
            : 'Explicá por qué este contenido debería ser compartido con el equipo (mínimo 10 caracteres)...'}
          className="font-mono text-sm resize-none"
          rows={4}
        />
        {!isAdmin && reason.length > 0 && reason.trim().length < 10 && (
          <p className="text-xs text-destructive font-mono">Mínimo 10 caracteres</p>
        )}
      </div>

      {/* Actions */}
      <div className="flex items-center justify-end gap-3 pt-1">
        <Button
          type="button"
          variant="ghost"
          size="sm"
          className="font-mono text-xs"
          onClick={onSuccess}
          disabled={isPending}
        >
          Cancelar
        </Button>
        <Button
          type="submit"
          size="sm"
          className={cn(
            'font-mono text-xs gap-1.5',
            isAdmin && 'bg-emerald-600 hover:bg-emerald-500 text-white',
          )}
          disabled={!canSubmit || isPending}
        >
          {isPending ? (
            <Loader2 className="h-3.5 w-3.5 animate-spin" />
          ) : isAdmin ? (
            <CheckCircle2 className="h-3.5 w-3.5" />
          ) : null}
          {isPending ? 'Enviando…' : isAdmin ? 'Publicar ahora' : 'Enviar solicitud'}
        </Button>
      </div>
    </form>
  )
}
