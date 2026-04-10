# The Sentient Architect — Frontend Implementation Log

**Spec de Referencia:** `docs/superpowers/specs/2026-04-10-frontend-design.md`  
**Estado:** ✅ Fases 1-4 Completadas, Fase 5 (Tareas 0-3) Completadas — 2026-04-10  
**Test suite:** 81 tests total — **78 passing** (3 pre-existing failures in guardian.test.ts)

## Status Legend
- ✅ Completed
- 🔄 In Progress
- 📋 Planned
- 🐛 Issue Found
- 🛑 Blocked

---

## Observaciones Arquitectónicas (The Architect's Guardrails)

Para que el código no se desvíe del diseño original, estas reglas aplican en todas las fases:
1. **RSC First:** Server Components por defecto. Solo se usa `'use client'` estrictamente donde hay hooks, eventos manuales o UI interactiva.
2. **SignalR Singleton:** JAMÁS inicializar la conexión directa en un componente de React. Usar un Singleton (ej: manejado en un store de Zustand o a nivel módulo).
3. **Typography Stack:** *Inter* para el cuerpo normal. **Fira Code** para encabezados, menús, etiquetas técnicas, consola y reportes (identidad "Architectural").
4. **Híbrido de Acciones:** El chat NO envía mensajes a través del socket. El envío se hace por una *Server Action* (para validar con Zod y JWT), el *Socket (SignalR)* es solo para lectura / *streaming* del LLM.
5. **Types From Backend:** Nada de typear interfaces a mano. Todo viene generado por `openapi-typescript` consumiendo el `v1.json` del backend.
6. **Zod Centralizado:** Todos los esquemas de validación viven en `lib/schemas.ts` para poder importarlos tanto en los formularios cliente interactivos como en las *Server Actions*.
7. **Ghost Loading:** Aprovechar la bondad de los RSC. Implementar siempre un `loading.tsx` visualmente exacto para evitar layouts shifts e imprimir feedback instantáneo de navegación.

---

## Phase 1 — Foundation (Los Cimientos)

| Tarea | Archivo(s) | Estado | Notas |
|---|---|---|---|
| 0. Setup Vitest + MSW | `vitest.config.ts`, `src/mocks/` | ✅ | Vitest + RTL + MSW v2 node server. Handlers para auth, knowledge y conversations. 13/13 tests pasando. |
| 1. Scaffold Next.js 15 | `src/app/` | ✅ | App Router, `src/` dir, `@/*` alias. Next.js 16.2 / React 19.2. |
| 2. Dependencias del Spec | `package.json` | ✅ | zustand@5, react-query@5, @microsoft/signalr@10, next-safe-action@7, zod@4, next-themes, clsx, tailwind-merge. |
| 3. Typography Fira Code + Inter | `app/layout.tsx` | ✅ | `Inter` → `--font-inter`. `Fira_Code` → `--font-fira-code`. Inyectadas como CSS vars. H1-H6 + `.font-heading` + `.font-mono` usan Fira Code. |
| 4. Setup Theme (next-themes) | `app/layout.tsx`, `globals.css` | ✅ | ThemeProvider defaultTheme="dark". Paleta "Dark/Neon Purple": violet `#8b5cf6` como primary/accent. CSS tokens en oklch para light + dark. |
| 5. CLI shadcn/ui base | `components/ui/` | ✅ | button, input, card, dialog, sheet, badge, separator, skeleton, sonner, label, textarea, select, dropdown-menu, avatar, scroll-area, tabs, tooltip. |
| 6. Type Safety Pipeline | `lib/api.types.ts` | ✅ | Stub types manuales alineados con `docs/API_CONTRACTS.md`. Script `npm run types:generate` configurado para regenerar desde el backend vivo. |
| 7. API Client Typed base | `lib/api-client.ts` | ✅ | JWT auth header, refresh automático en 401, cola de refresh, Result Pattern `ApiSuccess<T> / ApiError`. Evento `sa:unauthorized` para el store. |
| 8. Zod Schemas Centrales | `lib/schemas.ts` | ✅ | Auth, Knowledge, Consultant, Guardian, Profile, Admin. Types inferidos exportados. |
| 9. Auth Layer (Zustand + JWT) | `lib/auth.ts` | ✅ | `login()`, `register()`, `logout()`. `getToken()` exportado para signalr.ts. |
| 10. SignalR Base Singleton | `lib/signalr.ts` | ✅ | `getHubConnection(hubName)`: Map<HubName, HubConnection>. `startHub()`, `stopHub()`, `getHubState()`. Reconnect exponential backoff (máx 30s). Nunca instanciado en componentes. |
| 11. Rutas Públicas de Auth | `app/(public)/login/page.tsx`, `app/(public)/register/page.tsx` | ✅ | RSC shells con shadcn/ui Cards. Sin `'use client'`. |
| 12. Protección de Rutas | `middleware.ts` | ✅ | Cookie `sa_auth` como señal Edge-compatible. Redirect `/login?from=...` para rutas protegidas. Redirect `/` para rutas de auth cuando ya logueado. |

---

## Phase 2 — App Shell (El Layout / Skeleton)

| Tarea | Archivo(s) | Estado | Notas |
|---|---|---|---|
| 0. Test Hooks de Layout | `src/__tests__/layout/` | ✅ | 7 tests Sidebar + 6 tests ConsultantPanel — 26/26 passing. |
| 1. Configurar Zustand Store | `store/ui-store.ts` | ✅ | Ya completo desde Phase 1. sidebarOpen, consultantPanelOpen, setters, selectors. |
| 2. Sidebar Fijo Fira Code nav | `components/shared/layout/Sidebar.tsx` | ✅ | Logo, 4 grupos nav, badges violet/red, user card footer. Mobile Sheet. |
| 3. Topbar (ThemeToggle + actions)| `components/shared/layout/Topbar.tsx` | ✅ | Fira Code title desde pathname, Search ⌘K trigger, theme toggle, notifs, Consultant btn. |
| 4. Consultant Panel (Slide) | `components/shared/layout/ConsultantPanel.tsx`| ✅ | 380px slide-in, data-open attr, close btn → Zustand, welcome msg, textarea+send. |
| 5. AppShell Layout general | `components/shared/layout/AppShell.tsx` + `app/(dashboard)/layout.tsx` | ✅ | 3-col flex: Sidebar + flex-1 (Topbar+main) + ConsultantPanel. |
| 6. Responsive UI Handlers | `AppShell` | ✅ | md: CSS show/hide sidebar. xl: ConsultantPanel inline vs absolute overlay. |
| 7. Dashboard RSC Home | `app/(dashboard)/page.tsx` | ✅ | Greeting, quick actions, 4 StatCards, knowledge table, trends, pending approvals. |
| 8. Dashboard Ghost Loaders | `app/(dashboard)/loading.tsx` | ✅ | Skeleton layout calca exactamente el dashboard: header, stats, table, right col. |
| 9. Error Boundary & Suspense | `app/(dashboard)/error.tsx` | ✅ | AlertTriangle + retry button, logs error, 'use client' requerido por Next.js. |

---

## Phase 3 — Core Features (Los 4 Pilares - CRUD y View)

### 3A. Knowledge Brain
| Tarea | Archivo(s) | Estado | Notas |
|---|---|---|---|
| 0. Zod Validations Tests | `src/__tests__/features/brain.test.ts` | ✅ | 14 tests — ingestKnowledgeSchema, knowledgeSearchSchema, publishRequestSchema. 59/59 suite pasando. |
| 1. Tabla de Conocimientos | `features/brain/components/KnowledgeTable.tsx` | ✅ | TanStack Query. Type badges, status dots, pagination, row actions (delete + publish dialog). |
| 2. Ingesta Híbrida (Action) | `features/brain/actions.ts` | ✅ | `ingestKnowledgeAction` + `deleteKnowledgeAction` + `publishKnowledgeAction`. authedActionClient + Zod. |
| 3. Buscador Semántico RAG | `features/brain/components/SearchBar.tsx` | ✅ | Debounce 400ms, POST `/api/v1/knowledge/search`, dropdown with results. |
| 4. IngestDialog | `features/brain/components/IngestDialog.tsx` | ✅ | react-hook-form + zodResolver, tag manager, invalidates knowledge cache on success. |
| 5. PublishDialog | `features/brain/components/PublishDialog.tsx` | ✅ | Reason textarea, publishKnowledgeAction wired. |
| 6. Hook + Query Keys | `features/brain/hooks/useKnowledge.ts` | ✅ | useKnowledgeItems, useKnowledgeTags, useKnowledgeSearch, useInvalidateKnowledge. |
| 7. Brain Page RSC | `app/(dashboard)/brain/page.tsx` | ✅ | RSC shell + KnowledgeTableWrapper (client). Brain icon + RAG badge. |
| 8. Brain Ghost Loader | `app/(dashboard)/brain/loading.tsx` | ✅ | Skeleton mimics table layout (header + 8 rows + toolbar). |
| 9. MSW Knowledge Handlers | `mocks/handlers/knowledge.handlers.ts` | ✅ | 5 rich mock items, v1 paths, filtering, fuzzy search, tags endpoint. |

### 3B. Architecture Consultant
| Tarea | Archivo(s) | Estado | Notas |
|---|---|---|---|
| 0. Unit Tests Actions | `src/__tests__/features/consultant.test.ts` | ✅ | 10 tests — createConversationSchema + sendMessageSchema. |
| 1. Estructura de Historial | `features/consultant/hooks/useConversations.ts` | ✅ | useConversations (list), useConversation (detail + messages). |
| 2. Message Bubbles Render | `features/consultant/components/ChatPanel.tsx` | ✅ | User (bg-primary right) vs AI (bg-card left) bubbles. TypingIndicator (3-dot pulse). Auto-scroll. Optimistic messages. |
| 3. Hybrid Chat Input (POST) | `features/consultant/actions.ts` | ✅ | `createConversationAction` (maps title→objective), `sendMessageAction`, `archiveConversationAction`. |
| 4. Conversation List | `features/consultant/components/ConversationList.tsx` | ✅ | Groups Active/Anteriores. Archive on hover. New conversation button. |
| 5. ConsultantPanel updated | `components/shared/layout/ConsultantPanel.tsx` | ✅ | Now renders ChatPanel + ConversationList (via view toggle) instead of static shell. |
| 6. Consultant Page | `app/(dashboard)/consultant/page.tsx` | ✅ | Full-screen two-panel layout (list left, chat right) via ConsultantView client component. |
| 7. Consultant Ghost Loader | `app/(dashboard)/consultant/loading.tsx` | ✅ | Skeleton matches two-panel layout. |
| 8. MSW Conversation Handlers | `mocks/handlers/conversation.handlers.ts` | ✅ | 3 rich mock convs, v1 paths, message history. |

### 3C. Code Guardian
| Tarea | Archivo(s) | Estado | Notas |
|---|---|---|---|
| 0. Unit Tests Actions | `src/__tests__/features/guardian.test.ts` | ✅ | 7 tests — submitRepoSchema (GitHub URL regex, trustLevel defaults). |
| 1. Reporte Visual de Stats | `features/guardian/components/AnalysisReport.tsx` | ✅ | 4 score gauges (Overall/Security/Quality/Maintainability) + findings table with severity badges. |
| 2. Repositorio SubmitAction | `features/guardian/actions.ts` | ✅ | `submitRepoAction` (maps repositoryUrl→gitUrl) + `reanalyzeAction`. |
| 3. Submit Form | `features/guardian/components/SubmitRepoForm.tsx` | ✅ | URL + trustLevel select + notes, react-hook-form + Zod, useAction. |
| 4. Hooks | `features/guardian/hooks/useRepositories.ts` | ✅ | useRepositories, useRepositoryAnalysis, useFindings. |
| 5. Guardian Page | `app/(dashboard)/guardian/page.tsx` | ✅ | Two-column grid: form+repo list left, AnalysisReport right. Click repo to view analysis. |
| 6. Guardian Ghost Loader | `app/(dashboard)/guardian/loading.tsx` | ✅ | Two-column skeleton with gauge + table placeholders. |
| 7. MSW Repository Handlers | `mocks/handlers/repository.handlers.ts` | ✅ | POST (202 queue), GET list, GET analysis (dotnet/aspire with scores), GET findings, POST reanalyze. |

### Infrastructure Added in Phase 3
| Item | File | Notes |
|---|---|---|
| QueryClientProvider | `components/shared/Providers.tsx` | Client boundary. staleTime 60s, retry 1. |
| Safe Action clients | `lib/action-client.ts` | `actionClient` (public) + `authedActionClient` (reads `sa_token` cookie via next/headers). |
| Auth cookie | `lib/auth.ts` | Login writes `sa_token` cookie (SameSite=Strict). Logout clears it. |
| MSW handlers index | `mocks/handlers.ts` | Updated to include repositoryHandlers. |

### 3D. Trends Radar & 3E. Admin Panel
| Tarea | Archivo(s) | Estado | Notas |
|---|---|---|---|
| 0. Types & Schemas | `lib/api.types.ts`, `lib/schemas.ts` | ✅ | `Trend` (tractionLevel, relevanceScore, sources, TrendSnapshot), `PublishRequest` (nested knowledgeItem/requestedBy), `reviewPublishRequestSchema` con `.refine()`. |
| 1. MSW Handlers Trends | `mocks/handlers/trends.handlers.ts` | ✅ | 10 mock trends con filtros por category/traction/minRelevance. GET /api/v1/trends, GET /:id/snapshots, POST admin/trends/sync. |
| 2. MSW Handlers Admin | `mocks/handlers/admin.handlers.ts` | ✅ | 5 mock publish requests (3 Pending, 1 Approved, 1 Rejected). GET + PATCH con 400ms latencia simulada. GET /admin/users. |
| 3. Hooks Trends | `features/trends/hooks/useTrends.ts` | ✅ | `useTrends(filters)` staleTime 5min + `useTrendSnapshots(id)`. Constantes `TREND_CATEGORIES`, `TRACTION_LEVELS`. |
| 4. Hooks Admin | `features/admin/hooks/usePublishRequests.ts` | ✅ | `usePublishRequests(status?)` + `useInvalidatePublishRequests()`. |
| 5. Admin Server Action | `features/admin/actions.ts` | ✅ | `reviewPublishRequestAction` — authedActionClient + PATCH /api/v1/admin/publish-requests/:id. |
| 6. TrendsTable | `features/trends/components/TrendsTable.tsx` | ✅ | Filter bar (category + traction), `ScoreBar` por traction level, badges Emerging=sky/Growing=violet/Mainstream=emerald/Declining=red, skeleton rows, botón "Forzar Scan". |
| 7. PublishRequestsView | `features/admin/components/PublishRequestsView.tsx` | ✅ | **React 19 `useOptimistic`** — approve/reject actualiza fila antes del POST. `RejectDialog` con validación mínima. Tabs Pending/Approved/Rejected/All con live count badge. |
| 8. Trends page | `app/(dashboard)/trends/page.tsx` + `loading.tsx` | ✅ | RSC shell con TrendingUp icon + TrendsTable client component. Skeleton matching. |
| 9. Admin page | `app/(dashboard)/admin/publish-requests/page.tsx` + `loading.tsx` | ✅ | **Role guard** — lee `user.role` de Zustand. Redirige a /login si no hay usuario. `AccessDenied` si role !== 'Admin'. |

---

## Phase 4 — Real-Time Layer (El Sistema Nervioso - SignalR)

*(Aclaración: El backend ya provee los 3 hubs, está "Ready". La seguridad faltante del lado del backend no bloquea el progreso).*

| Tarea | Archivo(s) | Estado | Notas |
|---|---|---|---|
| 0. Integrations Test SignalR | `__tests__/signalr/streaming.test.tsx` | ✅ | Hub stub (EventEmitter pattern). Tests: IngestProgress (5) + AnalysisLiveLog (5). |
| 1. Chat Streaming AI Tokens | `features/consultant/components/ChatPanel.tsx`, `hooks/useHub.ts` | ✅ | `useHub` lifecycle hook + ref buffer + 60ms flush interval. Live bubble con cursor animado. |
| 2. Progress Ingest Dashboard | `features/brain/components/IngestProgress.tsx` | ✅ | Wired en `KnowledgeTableWrapper`. Auto-dismiss 4s/6s. Estados: primary/emerald/destructive. |
| 3. Health Analysis Live Log | `features/guardian/components/AnalysisLiveLog.tsx`, `GuardianView.tsx` | ✅ | Log monospace con timestamps. Filtrado por `repositoryId`. `onComplete` invalida cache. |
| Connection Indicator | `components/shared/layout/Topbar.tsx` | ✅ | Dot real mapeado a `hubStatus['conversation']`: verde/ámbar/rojo/celeste según HubConnectionState. |

---

## Phase 5 — Resiliency & 10/10 Layer (Capa "Premium Architect")

| Tarea | Archivo(s) | Estado | Notas |
|---|---|---|---|
| 0. Stress Test Retry Queue | `__tests__/resiliency/offline-queue.test.tsx` | ✅ | **11 tests** — store actions (enqueue/dequeue/clear/incrementRetry/unique IDs) + hook (flush on reconnect, no flush on stable Connected, no flush on empty queue). 11/11 passing. |
| 1. Zero-Latency UI (Chat) | `features/consultant/components/ChatPanel.tsx` | ✅ | `useOptimistic(serverMessages, reducer)` + `useTransition`. `streamCompleteRef` mantiene la transición abierta hasta que SignalR ReceiveComplete resuelve la promesa. Auto-rollback en error. |
| 2. Zero-Latency Acciones | `features/admin/components/PublishRequestsView.tsx` | ✅ | `useOptimistic` + `startTransition(async () => { updateOptimistic(...); await executeAsync(...) })`. Approve/Reject sin flash. |
| 3. Offline Queue & Retry | `store/ui-store.ts`, `hooks/useOfflineQueue.ts` | ✅ | `OfflineQueueItem` type, `offlineQueue[]` en Zustand (persistido). `enqueue/dequeue/clear/incrementRetry`. `useOfflineQueue` detecta transición Disconnected→Connected y flushea la cola. Toast Fira Code + WifiOff badge en ChatPanel. |
| 4. Cancel Generation UI | `ChatPanel.tsx` | 📋 | Botón "Stop" atado al token cancellation del SignalR/API. |

---

## Phase 6 — Polish, CI/CD & Delivery

| Tarea | Archivo(s) | Estado | Notas |
|---|---|---|---|
| 0. Suite Playwright E2E | `tests/e2e/` | 📋 | Trazar Login -> Ingesta Repo -> Streaming Chat -> CI/CD Gate prior to Docker. |
| 1. Landing Frontend (SEO) | `app/(public)/page.tsx` | 📋 | Una landing espectacular usando Fira Code en banners y tipografía limplia. |
| 2. Lighthouse & A11y Audit | Todo el proyecto | 📋 | Garantizar 95%+. Foco en ARIA Nav. Radix provee mucho by default, solo no romperlo. |
| 3. Command Palette / Cmd+K | `components/shared/CommandPalette.tsx` | 📋 | Intercambio rápido de páginas o invocación al Semantic Brain RAG. |
| 4. Dockerization | `Dockerfile` | 📋 | Generar el Multi-stage build (pnpm/npm + next build). |

---

## Frontend QA & Testing Strategy

Se mantienen tres niveles de granularidad equivalentes a la solidez del backend .NET:

1. **Unit Tests (Lógica Pura - Vitest + RTL)**: Aislar utils (`lib/utils.ts`), parsers de Zustand y Server Actions (validación con schemas de Zod en `lib/schemas.ts`).
2. **Integration Tests (Componentes + Server - Vitest + MSW)**: Probar que TanStack Query y zustand stores responden bien a llamadas HTTP simuladas (Mock Service Worker). Caso vital: aislar el SignalR Singleton y testar que no duplique streams.
3. **E2E Tests (Flujo de Negocio - Playwright)**: Garantizar el Happy Path principal: *Login -> Enviar Repo a escáner -> Preguntar por arquitectura en chat -> ver AI stream.*

## Technical Debt / Follow-ups (Frontend)
_Sin deudas pendientes actualmente. La estrategia de testeo preventivo en Tarea 0 garantiza cobertura inicial continua._
