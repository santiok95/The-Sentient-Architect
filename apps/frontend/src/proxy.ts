/**
 * proxy.ts
 * Route protection for all (dashboard) routes.
 *
 * Strategy: JWT is stored in localStorage (client-side only).
 * Middleware runs on Edge — it cannot read localStorage.
 * We use an httpOnly cookie `sa_auth` set during login as the server-side signal.
 *
 * Flow:
 * - Protected routes: redirect to /login if sa_auth cookie is absent.
 * - Auth routes (/login, /register): redirect to / if sa_auth cookie is present (already logged in).
 */
import { type NextRequest, NextResponse } from 'next/server'

const PROTECTED_PATHS = [
  '/',
  '/brain',
  '/consultant',
  '/guardian',
  '/trends',
  '/admin',
]

const AUTH_PATHS = ['/login']

const AUTH_COOKIE = 'sa_token'

function isProtected(pathname: string): boolean {
  return PROTECTED_PATHS.some(
    (p) => pathname === p || pathname.startsWith(`${p}/`),
  )
}

function isAuthRoute(pathname: string): boolean {
  return AUTH_PATHS.some(
    (p) => pathname === p || pathname.startsWith(`${p}/`),
  )
}

export function proxy(request: NextRequest) {
  const { pathname } = request.nextUrl
  const authCookie = request.cookies.get(AUTH_COOKIE)
  const isLoggedIn = !!authCookie?.value

  // Already logged in → redirect away from auth pages
  if (isAuthRoute(pathname) && isLoggedIn) {
    return NextResponse.redirect(new URL('/', request.url))
  }

  // Not logged in → redirect to login
  if (isProtected(pathname) && !isLoggedIn) {
    const loginUrl = new URL('/login', request.url)
    loginUrl.searchParams.set('from', pathname)
    return NextResponse.redirect(loginUrl)
  }

  return NextResponse.next()
}

export const config = {
  matcher: [
    /*
     * Match all request paths EXCEPT:
     * - _next/static (static files)
     * - _next/image (image optimization)
     * - favicon.ico, public assets
     * - API routes
     */
    '/((?!_next/static|_next/image|favicon.ico|.*\\.(?:svg|png|jpg|jpeg|gif|webp)$).*)',
  ],
}
