import type { Metadata } from 'next'
import Link from 'next/link'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card'

export const metadata: Metadata = { title: 'Crear cuenta' }

export default function RegisterPage() {
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
          <p className="text-sm text-muted-foreground">Creá tu workspace de desarrollo</p>
        </div>

        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="font-heading text-base">Nueva cuenta</CardTitle>
            <CardDescription className="text-xs">
              Completá los datos para empezar.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-1.5">
              <Label htmlFor="displayName" className="text-xs font-medium">
                Nombre para mostrar
              </Label>
              <Input
                id="displayName"
                name="displayName"
                type="text"
                placeholder="Juan Pérez"
                autoComplete="name"
                required
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="email" className="text-xs font-medium">
                Email
              </Label>
              <Input
                id="email"
                type="email"
                name="email"
                placeholder="dev@example.com"
                autoComplete="email"
                required
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="password" className="text-xs font-medium">
                Contraseña
              </Label>
              <Input
                id="password"
                type="password"
                name="password"
                placeholder="••••••••"
                autoComplete="new-password"
                required
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="confirmPassword" className="text-xs font-medium">
                Confirmar contraseña
              </Label>
              <Input
                id="confirmPassword"
                type="password"
                name="confirmPassword"
                placeholder="••••••••"
                autoComplete="new-password"
                required
              />
            </div>
            <Button type="submit" className="w-full" size="sm">
              Crear cuenta
            </Button>
          </CardContent>
          <CardFooter className="justify-center pt-0">
            <p className="text-xs text-muted-foreground">
              ¿Ya tenés cuenta?{' '}
              <Link href="/login" className="text-primary underline underline-offset-2 hover:text-primary/80">
                Iniciar sesión
              </Link>
            </p>
          </CardFooter>
        </Card>
      </div>
    </div>
  )
}
