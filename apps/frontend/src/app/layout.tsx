import type { Metadata } from 'next'
import { Inter, Fira_Code } from 'next/font/google'
import { ThemeProvider } from 'next-themes'
import { Toaster } from '@/components/ui/sonner'
import { TooltipProvider } from '@/components/ui/tooltip'
import { Providers } from '@/components/shared/Providers'
import { MSWProvider } from '@/components/shared/MSWProvider'
import './globals.css'

// ── Typography Stack (Design Guardrail #3) ────────────────────────────────────
// Inter → UI body text
// Fira Code → headings, nav labels, technical identifiers, console-like contexts

const inter = Inter({
  variable: '--font-inter',
  subsets: ['latin'],
  display: 'swap',
})

const firaCode = Fira_Code({
  variable: '--font-fira-code',
  subsets: ['latin'],
  display: 'swap',
  weight: ['400', '500', '600', '700'],
})

export const metadata: Metadata = {
  title: {
    template: '%s | The Sentient Architect',
    default: 'The Sentient Architect',
  },
  description:
    'AI-powered developer knowledge management — Semantic Brain, Architecture Consultant, Code Guardian, Trends Radar.',
}

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode
}>) {
  return (
    <html
      lang="en"
      suppressHydrationWarning
      className={`${inter.variable} ${firaCode.variable} h-full`}
    >
      <head>
        <script
          dangerouslySetInnerHTML={{
            __html: `window.__API_BASE_URL = "${process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5291'}"`,
          }}
        />
      </head>
      <body className="min-h-full antialiased">
        <ThemeProvider
          attribute="class"
          defaultTheme="dark"
          enableSystem
          disableTransitionOnChange
        >
          <TooltipProvider>
            <MSWProvider>
              <Providers>
                {children}
              </Providers>
            </MSWProvider>
            <Toaster richColors closeButton />
          </TooltipProvider>
        </ThemeProvider>
      </body>
    </html>
  )
}

