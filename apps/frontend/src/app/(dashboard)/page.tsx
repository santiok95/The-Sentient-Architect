import type { Metadata } from 'next'
import { Activity, Brain, MessageSquare, Shield } from 'lucide-react'

export const metadata: Metadata = { title: 'Dashboard' }

// ─── Types ────────────────────────────────────────────────────────────────────

type IconColor = 'violet' | 'green' | 'blue' | 'yellow'
type StatusDot = 'done' | 'processing' | 'pending'
type TractionLevel = 'growing' | 'emerging' | 'mainstream'

// ─── Static data ──────────────────────────────────────────────────────────────

const RECENT_KNOWLEDGE: {
  title: string
  meta: string
  tags: readonly string[]
  scope: 'shared' | 'personal'
  status: StatusDot
}[] = [
  { title: 'CQRS + Event Sourcing in .NET 9', meta: 'Artículo · hace 2h', tags: ['CQRS', '.NET'], scope: 'shared', status: 'done' },
  { title: 'Vertical Slice Architecture Guide', meta: 'Documentación · hace 5h', tags: ['Architecture'], scope: 'shared', status: 'done' },
  { title: 'pgvector Performance Benchmarks', meta: 'Nota · Ayer', tags: ['PostgreSQL', 'AI'], scope: 'personal', status: 'processing' },
  { title: 'github.com/dotnet/aspire', meta: 'Repositorio · hace 2 días', tags: ['Aspire', '.NET'], scope: 'shared', status: 'done' },
  { title: 'Rate Limiting Patterns for APIs', meta: 'Artículo · hace 3 días', tags: ['API', 'Patterns'], scope: 'personal', status: 'pending' },
]

const TRENDS: { icon: string; name: string; cat: string; traction: TractionLevel }[] = [
  { icon: '🚀', name: '.NET Aspire', cat: 'Framework · Microsoft', traction: 'growing' },
  { icon: '🤖', name: 'Semantic Kernel 2.0', cat: 'AI Orchestration', traction: 'growing' },
  { icon: '⚡', name: 'Bun Runtime', cat: 'Runtime · JS', traction: 'emerging' },
  { icon: '🗄️', name: 'pgvector + HNSW', cat: 'Database · Vector', traction: 'mainstream' },
]

const PENDING_APPROVALS: {
  initials: string
  gradient: string
  title: string
  meta: string
}[] = [
  { initials: 'MG', gradient: 'from-blue-500 to-violet-500', title: 'Microservices Anti-Patterns in 2025', meta: 'por Marcos G. · Artículo · hace 1h' },
  { initials: 'LR', gradient: 'from-green-500 to-cyan-500', title: 'Clean Architecture Decision Log', meta: 'por Laura R. · Documentación · hace 3h' },
  { initials: 'JP', gradient: 'from-yellow-500 to-red-500', title: 'github.com/dotnet/extensions', meta: 'por Juan P. · Repositorio · Ayer' },
]

// ─── Style helpers ────────────────────────────────────────────────────────────

const ICON_COLOR: Record<IconColor, string> = {
  violet: 'bg-primary/15 text-primary',
  green: 'bg-green-500/15 text-green-500',
  blue: 'bg-blue-500/15 text-blue-500',
  yellow: 'bg-yellow-500/15 text-yellow-500',
}

const STATUS_DOT: Record<StatusDot, string> = {
  done: 'bg-green-500',
  processing: 'bg-blue-500 animate-pulse',
  pending: 'bg-yellow-500',
}

const TRACTION: Record<TractionLevel, string> = {
  growing: 'bg-green-500/15 text-green-500',
  emerging: 'bg-yellow-500/15 text-yellow-500',
  mainstream: 'bg-blue-500/15 text-blue-500',
}

const TRACTION_LABELS: Record<TractionLevel, string> = {
  growing: 'En crecimiento',
  emerging: 'Emergente',
  mainstream: 'Establecido',
}

// ─── Stat card ────────────────────────────────────────────────────────────────

function StatCard({
  label,
  value,
  delta,
  deltaVariant = 'positive',
  iconColor,
  icon,
}: {
  label: string
  value: string | number
  delta: string
  deltaVariant?: 'positive' | 'neutral'
  iconColor: IconColor
  icon: React.ReactNode
}) {
  return (
    <div className="bg-card border border-border rounded-xl p-[18px_20px] hover:border-primary transition-colors">
      <div className="flex items-center justify-between mb-3">
        <span className="text-[12px] text-muted-foreground font-medium">{label}</span>
        <div className={`w-[30px] h-[30px] rounded-lg flex items-center justify-center ${ICON_COLOR[iconColor]}`}>
          {icon}
        </div>
      </div>
      <div className="text-[26px] font-bold tracking-[-0.5px] text-foreground">{value}</div>
      <div className={`text-[12px] mt-1 ${deltaVariant === 'positive' ? 'text-green-500' : 'text-muted-foreground'}`}>
        {delta}
      </div>
    </div>
  )
}

// ─── Page ─────────────────────────────────────────────────────────────────────

export default function DashboardPage() {
  return (
    <div className="p-7 pb-10">
      {/* Page header */}
      <div className="mb-6">
        <h1 className="font-mono text-[22px] font-bold tracking-[-0.4px] text-foreground">
          Buenos días, Santiago 👋
        </h1>
        <p className="text-[13.5px] text-muted-foreground mt-0.5">
          Esto es lo que está pasando en tu plataforma hoy.
        </p>
      </div>

      {/* Quick actions */}
      <div className="flex gap-2.5 mb-7 flex-wrap">
        <button className="inline-flex items-center gap-1.5 px-3.5 py-2 rounded-lg text-[13px] font-medium bg-primary text-primary-foreground hover:bg-primary/90 transition-colors">
          <Brain className="w-3.5 h-3.5" />
          Agregar conocimiento
        </button>
        <button className="inline-flex items-center gap-1.5 px-3.5 py-2 rounded-lg text-[13px] font-medium bg-card border border-border text-foreground hover:bg-card/80 transition-colors">
          <MessageSquare className="w-3.5 h-3.5" />
          Nueva consulta
        </button>
        <button className="inline-flex items-center gap-1.5 px-3.5 py-2 rounded-lg text-[13px] font-medium bg-card border border-border text-foreground hover:bg-card/80 transition-colors">
          <Shield className="w-3.5 h-3.5" />
          Analizar repo
        </button>
      </div>

      {/* Stats grid — 2 cols on mobile, 4 on xl */}
      <div className="grid grid-cols-2 xl:grid-cols-4 gap-3.5 mb-7">
        <StatCard
          label="Ítems en la base"
          value={47}
          delta="↑ 3 agregados hoy"
          iconColor="violet"
          icon={<Brain className="w-[15px] h-[15px]" />}
        />
        <StatCard
          label="Conversaciones activas"
          value={2}
          delta="Última actividad hace 12m"
          deltaVariant="neutral"
          iconColor="blue"
          icon={<MessageSquare className="w-[15px] h-[15px]" />}
        />
        <StatCard
          label="Repos analizados"
          value={8}
          delta="↑ 1 esta semana"
          iconColor="green"
          icon={<Shield className="w-[15px] h-[15px]" />}
        />
        <StatCard
          label="Tendencias monitoreadas"
          value={23}
          delta="↑ 5 nuevas esta semana"
          iconColor="yellow"
          icon={<Activity className="w-[15px] h-[15px]" />}
        />
      </div>

      {/* Main 2-col grid — stacked on mobile, 1fr/380px on xl */}
      <div className="grid grid-cols-1 xl:grid-cols-[1fr_380px] gap-4">

        {/* Recent knowledge */}
        <div className="bg-card border border-border rounded-xl overflow-hidden">
          <div className="flex items-center justify-between px-5 py-4 border-b border-border">
            <span className="font-mono text-[13.5px] font-semibold text-foreground">
              Conocimiento compartido reciente
            </span>
            <span className="text-[12px] text-primary cursor-pointer hover:underline">
              Ver todo →
            </span>
          </div>
          <table className="w-full border-collapse">
            <thead>
              <tr>
                {['Ítem', 'Tags', 'Scope', 'Estado'].map((h) => (
                  <th
                    key={h}
                    className="text-left text-[11px] font-semibold text-muted-foreground uppercase tracking-[0.5px] px-5 py-2.5 border-b border-border"
                  >
                    {h}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {RECENT_KNOWLEDGE.map((item) => (
                <tr key={item.title} className="hover:bg-card/60 transition-colors">
                  <td className="px-5 py-3 border-b border-border/50 last:border-b-0">
                    <div className="text-[13px] font-medium text-foreground">{item.title}</div>
                    <div className="text-[11.5px] text-muted-foreground">{item.meta}</div>
                  </td>
                  <td className="px-5 py-3 border-b border-border/50">
                    <div className="flex gap-1 flex-wrap">
                      {item.tags.map((tag) => (
                        <span
                          key={tag}
                          className="text-[11px] font-medium px-2 py-0.5 rounded-full border border-border text-muted-foreground bg-input"
                        >
                          {tag}
                        </span>
                      ))}
                    </div>
                  </td>
                  <td className="px-5 py-3 border-b border-border/50">
                    <span
                      className={`text-[10px] font-semibold px-1.5 py-0.5 rounded-full ${
                        item.scope === 'shared'
                          ? 'bg-green-500/15 text-green-500'
                          : 'bg-blue-500/15 text-blue-500'
                      }`}
                    >
                      {item.scope === 'shared' ? 'Compartido' : 'Personal'}
                    </span>
                  </td>
                  <td className="px-5 py-3 border-b border-border/50">
                    <span className={`inline-block w-1.5 h-1.5 rounded-full ${STATUS_DOT[item.status]}`} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {/* Processing bar */}
          <div className="flex items-center gap-2.5 px-5 py-2.5 bg-blue-500/10 border-t border-border text-[12px] text-blue-400">
            <span className="inline-block w-1.5 h-1.5 rounded-full bg-blue-500 animate-pulse flex-shrink-0" />
            pgvector Performance Benchmarks — Generando embeddings (72%)
          </div>
        </div>

        {/* Right column */}
        <div className="flex flex-col gap-4 md:grid md:grid-cols-2 xl:grid xl:grid-cols-1">

          {/* Trending */}
          <div className="bg-card border border-border rounded-xl overflow-hidden">
            <div className="flex items-center justify-between px-5 py-4 border-b border-border">
              <span className="font-mono text-[13.5px] font-semibold text-foreground">
                Trending This Week
              </span>
              <span className="text-[12px] text-primary cursor-pointer hover:underline">
                View all →
              </span>
            </div>
            {TRENDS.map((trend) => (
              <div
                key={trend.name}
                className="flex items-center gap-3 px-5 py-3 border-b border-border/50 last:border-b-0"
              >
                <div className="w-[34px] h-[34px] rounded-lg bg-input flex items-center justify-center text-base flex-shrink-0">
                  {trend.icon}
                </div>
                <div className="flex-1 min-w-0">
                  <div className="text-[13px] font-medium text-foreground">{trend.name}</div>
                  <div className="text-[11.5px] text-muted-foreground">{trend.cat}</div>
                </div>
                <span className={`text-[10.5px] font-semibold px-2 py-0.5 rounded-full whitespace-nowrap ${TRACTION[trend.traction]}`}>
                  {TRACTION_LABELS[trend.traction]}
                </span>
              </div>
            ))}
          </div>

          {/* Pending approvals */}
          <div className="bg-card border border-border rounded-xl overflow-hidden">
            <div className="flex items-center justify-between px-5 py-4 border-b border-border">
              <span className="font-mono text-[13.5px] font-semibold text-foreground">
                Aprobaciones pendientes
              </span>
              <span className="text-[11px] font-semibold px-2 py-0.5 rounded-full bg-destructive/15 text-destructive">
                3
              </span>
            </div>
            {PENDING_APPROVALS.map((req) => (
              <div
                key={req.title}
                className="flex items-start gap-3 px-5 py-3.5 border-b border-border/50 last:border-b-0"
              >
                <div
                  className={`w-7 h-7 rounded-full bg-gradient-to-br ${req.gradient} flex items-center justify-center text-white text-[11px] font-semibold flex-shrink-0`}
                >
                  {req.initials}
                </div>
                <div className="flex-1 min-w-0">
                  <div className="text-[13px] font-medium text-foreground truncate">{req.title}</div>
                  <div className="text-[11.5px] text-muted-foreground mt-0.5">{req.meta}</div>
                  <div className="flex gap-1.5 mt-2">
                    <button className="px-2.5 py-0.5 rounded-md text-[11.5px] font-medium bg-green-500/15 text-green-500 cursor-pointer hover:bg-green-500/25 transition-colors">
                      ✓ Aprobar
                    </button>
                    <button className="px-2.5 py-0.5 rounded-md text-[11.5px] font-medium bg-destructive/15 text-destructive cursor-pointer hover:bg-destructive/25 transition-colors">
                      ✕ Rechazar
                    </button>
                  </div>
                </div>
              </div>
            ))}
          </div>

        </div>
      </div>
    </div>
  )
}
