<div align="center">

# 🏗️ The Sentient Architect

### *El segundo cerebro definitivo para arquitectos de software.*

[![.NET 9](https://img.shields.io/badge/.NET-9-512bd4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Next.js 15](https://img.shields.io/badge/Next.js-15-000?style=for-the-badge&logo=next.js)](https://nextjs.org/)
[![React 19](https://img.shields.io/badge/React-19-61dafb?style=for-the-badge&logo=react&logoColor=black)](https://react.dev/)
[![PostgreSQL + pgvector](https://img.shields.io/badge/PostgreSQL-pgvector-336791?style=for-the-badge&logo=postgresql&logoColor=white)](https://github.com/pgvector/pgvector)
[![Semantic Kernel](https://img.shields.io/badge/Semantic_Kernel-1.x-8A2BE2?style=for-the-badge)](https://github.com/microsoft/semantic-kernel)
[![SignalR](https://img.shields.io/badge/SignalR-Realtime-005FB8?style=for-the-badge)](https://learn.microsoft.com/aspnet/core/signalr)

> **Centralizá conocimiento. Auditá código. Decidí con evidencia. Todo desde un mismo lugar.**

[🎬 Ver guía visual](./HOW-TO-USE.md) · [🏛️ Decisiones arquitectónicas](./docs/ARCHITECTURE_DECISIONS.md) · [📖 Contexto del proyecto](./docs/PROJECT_CONTEXT.md) · [🔌 API contracts](./docs/API_CONTRACTS.md)

</div>

---

## ⚡ ¿Qué es esto?

**The Sentient Architect** es una plataforma que une **RAG semántico**, **análisis estático con IA** y **agentes conversacionales** para darle a un equipo de desarrollo un compañero inteligente que recuerda todo lo que aprendieron, audita su código, y los ayuda a decidir cuando aparece un problema nuevo.

No reemplaza al arquitecto. Le da el contexto que necesita para tomar mejores decisiones, más rápido.

> 🎬 **¿Querés ver cómo se siente usarla?** Mirá la [guía visual en HOW-TO-USE.md](./HOW-TO-USE.md) — cada pantalla explicada con capturas.

---

## 🎯 Los problemas que resolvemos

| Dolor | Cómo lo atacamos |
|-------|------------------|
| 🧠 **El conocimiento se fragmenta** entre Slack, Confluence, cabezas y gists perdidos. | El **Semantic Brain** indexa todo con embeddings y búsqueda vectorial. |
| 🛡️ **El código se desvía** de los patrones originales sin que nadie se dé cuenta. | El **Code Guardian** corre análisis estático + IA sobre cada repo. |
| 🤖 **Decidir consume horas** cuando no tenés el contexto fresco. | El **Architecture Consultant** conversa con vos, con acceso a tu Brain y tus repos. |
| 📡 **El ecosistema cambia más rápido de lo que podemos seguir.** | El **Trends Radar** mantiene snapshots curados de frameworks y herramientas. |

---

## 🎭 ¿Para quién?

<table>
<tr>
<td width="50%" valign="top">

### 👶 Para el Junior
**Tu guardaespaldas arquitectónico.**
Te permite escribir código con confianza, sabiendo que las decisiones ya tomadas por el equipo están a un click. Menos dudas, menos "¿cómo hicimos esto la última vez?", más ownership.

</td>
<td width="50%" valign="top">

### 👴 Para el Senior
**Tu radar de vanguardia.**
En un mundo donde sale un framework por semana, actúa como filtro. Te muestra qué tecnologías ganan tracción real y cuáles son humo. Vos estrategia, él monitoreo.

</td>
</tr>
</table>

---

## 🧩 Los 4 pilares

### 🧠 Semantic Brain
Base de conocimiento con **RAG** (Retrieval-Augmented Generation).
- Chunks de 500–800 tokens con overlap, embeddings vía OpenAI `text-embedding-3-small`.
- **PostgreSQL + pgvector** con índice **HNSW** para búsqueda sub-lineal (O(log n)).
- Búsqueda híbrida: título + contenido + tags, con filtrado por scope (personal / compartido).

### 🛡️ Code Guardian
Auditor estático con asistencia de IA.
- Clone del repo a directorio efímero (nunca se persiste código fuente).
- **Roslyn** para análisis sintáctico C#; AST-based para TypeScript.
- Detección de hallazgos clasificados por severidad: `Critical`, `High`, `Medium`, `Low`.
- **Trust levels**: `External` (full security scan) vs `Internal` (quality-focused).

### 🤖 Architecture Consultant
Agente conversacional con **Semantic Kernel**.
- Streaming de tokens vía **SignalR** (respuesta fluida, sin spinners).
- Acceso a `SearchPlugin`, `ProfilePlugin`, `RepositoryContextPlugin`, `TrendsPlugin`.
- Context-aware: compone la respuesta con `UserProfile` + `ConversationSummary` + resultados RAG.
- **Sin exceptions para errores de negocio** — Result pattern end-to-end.

### 📡 Trends Radar
Monitoreo de ecosistema como `IHostedService`.
- Snapshots periódicos con ABOUT.md curado por tecnología.
- Niveles de tracción: `Emerging`, `Growing`, `Mainstream`, `Declining`.
- Consumido automáticamente por el Consultant cuando se pregunta por alternativas o modernización.

---

## 🛠️ Stack

<table>
<tr>
<td width="50%" valign="top">

### Backend (.NET 9, C# 13)
- **Clean Architecture** — 6 proyectos, dependencias inward-only.
- **Minimal APIs** — endpoints como módulos registrables.
- **EF Core 9** + PostgreSQL 16 + pgvector.
- **Semantic Kernel** para orquestación de agentes.
- **SignalR** para streaming en 3 hubs (`conversation`, `analysis`, `ingestion`).
- **ASP.NET Identity** con JWT bearer + roles (`Admin`, `User`).
- **Result Pattern** — nunca exceptions para control de flujo.

</td>
<td width="50%" valign="top">

### Frontend (Next.js 15, React 19)
- **App Router** con React Server Components.
- **Server Actions** + `next-safe-action` + Zod para mutations seguras.
- **TanStack Query v5** (server state) + **Zustand** (client state).
- **SignalR client** con connection singleton por hub.
- **openapi-typescript** — tipos del backend espejados automáticamente.
- **Tailwind v4** + **shadcn/ui** + **base-ui** para primitivos.

</td>
</tr>
</table>

---

## 🏛️ Filosofía de diseño

1. **Domain puro.** `SentientArchitect.Domain` tiene **cero** dependencias NuGet y **cero** `throw`. La validación vive en Application con Result pattern.
2. **No Repository pattern.** `DbContext` *es* el repositorio y la unit of work. Los use cases inyectan `IApplicationDbContext` directo (estilo Jason Taylor).
3. **Hybrid data flow.** Input por HTTP POST (seguridad + Zod). Output de alta latencia (streaming, progreso) por SignalR.
4. **Multi-tenant desde el día 1.** Todo item tiene `UserId` + `TenantId`. El contenido empieza personal y solo pasa a compartido con aprobación admin.
5. **Nunca ejecutamos código externo.** Guardian hace análisis 100% estático. El repo clonado se borra después.

---

## 📁 Estructura del monorepo

```
the-sentient-architect/
├── apps/
│   ├── backend/                              # Solución .NET 9 (Clean Architecture)
│   │   ├── SentientArchitect.Domain/         # Entidades, enums — zero dependencies
│   │   ├── SentientArchitect.Application/    # Use cases, interfaces, Result pattern
│   │   ├── SentientArchitect.Data/           # DbContext + configurations (DB-agnostic)
│   │   ├── SentientArchitect.Data.Postgres/  # Postgres-specific, migrations, pgvector
│   │   ├── SentientArchitect.Infrastructure/ # Identity, AI services, Semantic Kernel
│   │   ├── SentientArchitect.API/            # Minimal APIs, SignalR hubs, middleware
│   │   └── tests/                            # Unit + integration tests (Testcontainers)
│   │
│   └── frontend/                             # Next.js 15 (App Router)
│       └── src/
│           ├── app/                          # Routes: (public)/login · (dashboard)/*
│           ├── features/                     # Feature-first: brain · consultant · guardian · trends · admin
│           ├── components/ui/                # Primitivos (button, input, dialog…)
│           ├── lib/                          # auth, api-client, signalr, utils
│           ├── hooks/                        # useHub y utilidades globales
│           └── store/                        # Zustand stores
│
├── docs/                                     # Decisiones arquitectónicas, API, QA
├── docker-compose.yml                        # Postgres + backend + frontend, one-shot
└── HOW-TO-USE.md                             # Guía visual con capturas
```

---

## 🚀 Quickstart

### Opción 1 — Docker (todo levantado en 30 segundos)

```bash
cp .env.example .env
# Editá .env con tus keys (OPENAI_API_KEY, ANTHROPIC_API_KEY, JWT_KEY, etc.)
docker compose up -d
```

Abrí [http://localhost:3000](http://localhost:3000) → login con el admin seedeado.

### Opción 2 — Desarrollo local

**Backend:**
```bash
cd apps/backend
dotnet restore
dotnet ef database update -p SentientArchitect.Data.Postgres -s SentientArchitect.API
dotnet run --project SentientArchitect.API
```

**Frontend:**
```bash
cd apps/frontend
pnpm install
pnpm dev
```

---

## 🔑 Variables de entorno clave

| Variable | Para qué sirve |
|----------|----------------|
| `OPENAI_API_KEY` | Embeddings (`text-embedding-3-small`) para el Brain. |
| `ANTHROPIC_API_KEY` | Chat model del Consultant (default: `claude-haiku-4-5`). |
| `JWT_KEY` | Firma de tokens JWT. **Mínimo 32 chars.** |
| `ADMIN_EMAIL` / `ADMIN_PASSWORD` | Usuario admin seedeado al primer arranque. |
| `POSTGRES_USER` / `POSTGRES_PASSWORD` | Credenciales Postgres (Docker). |
| `NEXT_PUBLIC_API_URL` | URL pública del backend (default `http://localhost:5291`). |

> 🔒 Ver `.env.example` para la lista completa y defaults seguros.

---

## 🧪 Tests y calidad

```bash
# Backend
cd apps/backend
dotnet test tests/

# Frontend
cd apps/frontend
pnpm test              # Vitest + Testing Library
pnpm test:e2e          # Playwright (E2E)
pnpm typecheck         # tsc --noEmit estricto
```

Los tests de integración usan **Testcontainers** para levantar Postgres + pgvector reales — nada de mocks del store vectorial.

---

## 🛣️ Roadmap

- [x] Clean Architecture + dominio puro
- [x] RAG pipeline con pgvector + HNSW
- [x] Consultant con streaming SignalR
- [x] Code Guardian con Roslyn + AST TS
- [x] Trends Radar como `IHostedService`
- [x] Multi-tenant con scopes personal/compartido
- [x] Dockerización completa (dev + prod)
- [ ] Refresh token rotation
- [ ] Export/import de Brain en bundles firmados
- [ ] Integración nativa con GitHub Apps (auto-análisis en PR)

---

## 🤝 Contribuir

Este es un proyecto con convenciones fuertes. Antes de abrir PR, leé:

- [`CLAUDE.md`](./CLAUDE.md) — reglas generales del proyecto.
- [`.claude/rules/clean-architecture.md`](./.claude/rules/clean-architecture.md) — dependencias entre capas.
- [`.claude/rules/coding-standards.md`](./.claude/rules/coding-standards.md) — entity patterns, Result pattern, EF Core.
- [`.claude/rules/security.md`](./.claude/rules/security.md) — reglas de análisis estático.

---

<div align="center">

**Hecho con ☕, PostgreSQL y una obsesión por la arquitectura limpia.**

[🎬 Ver cómo se usa](./HOW-TO-USE.md) · [📖 Docs técnicas](./docs/) · [🐛 Reportar un bug](../../issues)

</div>
