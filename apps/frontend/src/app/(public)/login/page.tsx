import type { Metadata } from 'next'
import { Suspense } from 'react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { LoginForm } from './_components/LoginForm'

export const metadata: Metadata = { title: 'Iniciar sesión' }

/**
 * Login page — RSC outer shell, form interaction handled client-side.
 * No 'use client' here — the interactive form lives in a separate Client Component.
 */
export default function LoginPage() {
  return (
    <div className="min-h-screen flex items-center justify-center bg-background p-4">
      <div className="w-full max-w-sm space-y-6">
        {/* Logo / Brand */}
        <div className="text-center space-y-1">
          <div className="inline-flex items-center justify-center w-10 h-10 rounded-xl bg-gradient-to-br from-primary to-indigo-500 text-primary-foreground font-heading font-bold text-lg mb-2">
            S
          </div>
          <h1 className="font-heading text-xl font-semibold tracking-tight">
            The Sentient Architect
          </h1>
          <p className="text-sm text-muted-foreground">Ingresá a tu workspace</p>
        </div>

        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="font-heading text-base">Bienvenido</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <Suspense>
              <LoginForm />
            </Suspense>
          </CardContent>
        </Card>
      </div>
    </div>
  )
}
