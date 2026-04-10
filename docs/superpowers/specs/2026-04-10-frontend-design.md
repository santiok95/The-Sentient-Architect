# Frontend Design Spec — The Sentient Architect
**Date:** 2026-04-10  
**Status:** Approved  
**Branch:** features/UI-Design

---

## 1. Decision Summary

| Decision | Choice | Rationale |
|---|---|---|
| Framework | Next.js 15 (App Router) | SSR for public pages (landing, register), CSR for dashboard |
| Component library | shadcn/ui + Tailwind CSS | Owned code, no dependency lock-in, Radix UI accessibility |
| Theming | next-themes + CSS variables | User-selectable dark/light, persisted in profile |
| Server state | TanStack Query v5 | Cache, loading states, auto-revalidation |
| Client state | Zustand | UI-only state (sidebar, theme toggle) — minimal |
| Forms/mutations | next-safe-action + Zod | Result Pattern equivalent for the frontend |
| Real-time | @microsoft/signalr | Connect to ConversationHub, AnalysisHub, IngestionHub |
| Type safety | openapi-typescript (auto-gen from Scalar) | End-to-end types from .NET API spec |
| Architecture pattern | Feature-Based (Modular) | Mirrors Clean Architecture of the backend |

---

## 2. Project Structure

```
src/
├── app/                          # Next.js App Router — routing only
│   ├── (public)/                 # Route group: no auth required
│   │   ├── page.tsx              # Landing page (SSR, SEO-optimized)
│   │   ├── login/page.tsx
│   │   └── register/page.tsx
│   ├── (dashboard)/              # Route group: requires auth
│   │   ├── layout.tsx            # Shell: sidebar + topbar + consultant panel
│   │   ├── page.tsx              # Dashboard home (RSC)
│   │   ├── brain/page.tsx
│   │   ├── consultant/page.tsx
│   │   ├── guardian/page.tsx
│   │   ├── trends/page.tsx
│   │   └── admin/
│   │       ├── users/page.tsx
│   │       └── requests/page.tsx
│   ├── layout.tsx                # Root layout (providers, fonts)
│   └── globals.css
│
├── features/                     # Business logic by domain
│   ├── brain/
│   │   ├── components/           # KnowledgeTable, IngestForm, SearchBar, ItemDetail
│   │   ├── hooks/                # useKnowledgeItems, useIngestionProgress
│   │   ├── actions.ts            # Server Actions: ingest, delete, publishRequest
│   │   ├── api.ts                # GET /knowledge, POST /knowledge/search
│   │   └── types.ts              # Local feature types (extends generated)
│   ├── consultant/
│   │   ├── components/           # ChatPanel, MessageBubble, ContextBadge, TypingIndicator
│   │   ├── hooks/
│   │   │   ├── useConversation.ts  # HTTP POST + SignalR streaming (NO Server Action)
│   │   │   └── useConversations.ts # List/create conversations
│   │   ├── api.ts                # GET/POST /conversations
│   │   └── types.ts
│   ├── guardian/
│   │   ├── components/           # RepoSubmitForm, AnalysisReport, FindingsTable, ProgressBar
│   │   ├── hooks/
│   │   │   └── useAnalysisProgress.ts  # SignalR AnalysisHub listener
│   │   ├── actions.ts            # Server Actions: submitRepo, reanalyze
│   │   ├── api.ts                # GET /repositories, GET analysis/findings
│   │   └── types.ts
│   ├── trends/
│   │   ├── components/           # TrendCard, TractionBadge, SnapshotChart
│   │   ├── hooks/                # useTrends
│   │   ├── api.ts                # GET /trends, GET /trends/{id}/snapshots
│   │   └── types.ts
│   ├── profile/
│   │   ├── components/           # ProfileForm, SuggestionCard, TokenUsageChart
│   │   ├── hooks/                # useProfile, useProfileSuggestions
│   │   ├── actions.ts            # Server Actions: updateProfile, acceptSuggestion
│   │   ├── api.ts                # GET/PUT /profile
│   │   └── types.ts
│   └── admin/
│       ├── components/           # PublishRequestCard, UserTable, QuotaEditor
│       ├── hooks/                # usePublishRequests, useUsers
│       ├── actions.ts            # Server Actions: approveRequest, changeRole, setQuota
│       ├── api.ts                # GET /admin/publish-requests, PATCH role/quota
│       └── types.ts
│
├── components/
│   ├── ui/                       # shadcn/ui (owned code — edit freely)
│   └── shared/
│       ├── layout/               # AppShell, Sidebar, Topbar, ConsultantPanel
│       ├── feedback/             # Toast, ProgressBar, EmptyState, ErrorBoundary
│       └── data/                 # DataTable, Pagination, TagBadge, ScopeBadge
│
├── lib/
│   ├── api-client.ts             # Typed HTTP client: base URL, JWT header, refresh, error handling
│   ├── api.types.ts              # Auto-generated from Scalar (/openapi/v1.json) — DO NOT EDIT
│   ├── signalr.ts                # SignalR connection factory (singleton per hub)
│   ├── auth.ts                   # next-auth or JWT helpers, session management
│   └── utils.ts                  # cn(), formatDate(), truncate()
│
└── store/
    └── ui-store.ts               # Zustand: sidebarOpen, consultantPanelOpen, activeTheme
```

---

## 3. Layout Architecture

### Shell (3-column when Consultant is open)

```
┌─────────────┬──────────────────────────┬──────────────┐
│  Sidebar    │     Main Content         │  Consultant  │
│  (240px)    │     (flex-1)             │  Panel       │
│  fixed      │                          │  (380px)     │
│             │                          │  toggleable  │
└─────────────┴──────────────────────────┴──────────────┘
```

- **Sidebar**: Fixed left, 240px. Logo, 4 pillar nav items with badges, Admin section (role-gated), user card + settings at bottom.
- **Topbar** (within main): Page title, ⌘K search, theme toggle, notifications bell, **"Consultant" button** (violet, with chat icon + green activity dot).
- **Consultant Panel**: Right side, 380px. Toggled by the Consultant button. Header shows context mode badge (Auto / RepoBound / StackBound). Chat messages + typing indicator + input with send button.

### Responsive Behavior
- `< 1100px`: Consultant panel overlays main content (doesn't push it).
- `< 768px`: Sidebar becomes a bottom sheet or hamburger drawer. Consultant panel full-screen modal.

---

## 4. Key Screens

### Dashboard Home (RSC)
- Greeting + date
- Quick action buttons: Add Knowledge, New Consultation, Analyze Repo
- 4 stat cards: Knowledge Items, Active Conversations, Repos Analyzed, Trends Tracked
- Two columns: Recent Shared Knowledge table + right column (Trending This Week + Pending Approvals for Admin)
- Real-time ingestion progress bar via IngestionHub (SignalR)

### Knowledge Brain
- Search bar (semantic search → `POST /knowledge/search`)
- Filter row: type (Article/Note/Doc/Repo), scope (Personal/Shared), tags, status
- Knowledge items list with status indicators (Pending/Processing/Completed)
- Item detail panel (slide-in or separate page)
- Ingestion form: title, content/URL, type, tags — submits via Server Action
- Publish request button (for User role)

### Architecture Consultant
- Conversation list (left mini-panel or top tabs)
- Chat area: message history, streaming tokens via SignalR, stop generation button
- Context mode selector: Auto / RepoBound / StackBound / Generic
- Profile suggestion banner (non-intrusive) when AI detects profile changes
- Token usage indicator in footer

### Code Guardian
- Repository submission form: Git URL, trust level (Internal/External)
- Repository list with health scores and last analysis date
- Analysis detail: 4 score gauges (Overall, Security, Quality, Maintainability)
- Findings table with severity filter (Critical/High/Medium/Low) and category
- Real-time analysis progress via AnalysisHub: phases Cloning → Analyzing → ScanningDependencies → GeneratingReport

### Trends Radar
- Trends grid/list with traction badge (Emerging/Growing/Mainstream/Declining)
- Relevance score (from user profile matching)
- Trend detail: description + snapshot history chart
- Admin: Force sync button (triggers `POST /admin/trends/sync`)

### Admin
- Publish Requests queue: approve/reject with reason
- Users list: role change, quota override
- Token usage overview per user

---

## 5. Data Flow Patterns

### Pattern A — Read (RSC, no loading state needed)
```
page.tsx (Server Component)
  └── await apiClient.get('/knowledge')  ← direct fetch, server-side
        └── renders with data immediately
```

### Pattern B — Mutation (Server Action + Zod)
```
IngestForm (Client Component)
  └── useAction(ingestKnowledgeAction)    ← next-safe-action
        └── actions.ts validates with Zod
              └── calls apiClient.post('/knowledge')
                    └── returns { data } | { serverError } | { validationErrors }
                          └── Toast success/error
                                └── TanStack Query invalidates cache
```

### Pattern C — Real-time Chat (SignalR, NO Server Action)
```
useConversation.ts
  ├── POST /conversations/{id}/messages   ← direct HTTP (202 Accepted)
  └── signalr.on('ReceiveMessageChunk')  ← streams tokens to UI state
        └── signalr.on('MessageCompleted') ← finalizes, updates TanStack cache
```

### Pattern D — Async Progress (SignalR)
```
useAnalysisProgress.ts / useIngestionProgress.ts
  ├── subscribe to AnalysisHub / IngestionHub by knowledgeItemId
  └── update local state: phase, percentComplete
        └── ProgressBar component reads state
```

---

## 6. Type Safety Strategy

```bash
# Run after any .NET API change
npx openapi-typescript http://localhost:5000/openapi/v1.json -o src/lib/api.types.ts
```

- `api.types.ts` is auto-generated — never edited manually.
- `api-client.ts` uses `paths` and `components['schemas']` from generated types.
- Each feature's `types.ts` extends or picks from the generated types.
- Build fails if a DTO changes in .NET and types are not regenerated — caught at CI, not runtime.

---

## 7. Authentication Flow

- JWT stored in `httpOnly` cookie (not localStorage — XSS protection).
- `lib/auth.ts` handles token refresh via `POST /auth/refresh` on 401 response.
- Route protection: middleware in `middleware.ts` redirects unauthenticated users to `/login`.
- Role-based UI: Admin sections hidden via server-side role check in RSC (not just CSS visibility).

---

## 8. Component Rules

- **RSC by default.** Only add `'use client'` when: event handlers, browser APIs, hooks (useState, useEffect, SignalR), animated components.
- **No prop drilling.** Data fetched in RSC is passed as props one level deep max. TanStack Query handles everything else client-side.
- **shadcn/ui is your code.** `components/ui/` files are owned — modify Tailwind classes directly.
- **Never break Radix ARIA.** Do not remove `role`, `aria-*`, or keyboard handler props from shadcn primitives.
- **Error boundaries** on each feature route to isolate failures.

---

## 9. npm Package List

```json
{
  "dependencies": {
    "next": "15.x",
    "react": "19.x",
    "react-dom": "19.x",
    "tailwindcss": "4.x",
    "@tailwindcss/typography": "^0.5",
    "next-themes": "^0.4",
    "shadcn-ui": "latest (CLI)",
    "@tanstack/react-query": "^5",
    "zustand": "^5",
    "next-safe-action": "^7",
    "zod": "^3",
    "@microsoft/signalr": "^8",
    "openapi-typescript": "^7 (devDep)"
  },
  "devDependencies": {
    "typescript": "^5",
    "openapi-typescript": "^7",
    "@types/react": "^19",
    "eslint": "^9",
    "eslint-config-next": "15.x"
  }
}
```

---

## 10. What This Is Not

- No Redux, no MobX, no Context for server data.
- No Atomic Design folder structure (atoms/molecules/organisms).
- No direct `fetch()` calls scattered across components — always through `api-client.ts`.
- No `any` types — TypeScript strict mode on.
- No `localStorage` for auth tokens.
- No business logic in `app/` pages — pages are thin shells that import from `features/`.
