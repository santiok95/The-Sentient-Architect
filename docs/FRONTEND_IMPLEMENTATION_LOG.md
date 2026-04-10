# The Sentient Architect — Frontend Implementation Log

**Spec de Referencia:** `docs/superpowers/specs/2026-04-10-frontend-design.md`  
**Estado:** Iniciando Fase 1

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
| 0. Setup Vitest + MSW | `vitest.config.ts`, `mocks/` | 📋 | Definir infraestructura de tests unitarios y mockear APIs con MSW. |
| 1. Scaffold Next.js 15 | `src/app/` | 📋 | App Router, sin `src/pages`. Limpieza inicial. |
| 2. Dependencias del Spec | `package.json` | 📋 | shadcn/ui, tailwind 4, zustand 5, react-query 5, signalr 8, next-safe-action 7, zod. |
| 3. Typography Fira Code + Inter | `app/layout.tsx` | 📋 | Importación de Google Fonts e inyección de clases. |
| 4. Setup Theme (next-themes) | `app/layout.tsx`, `globals.css` | 📋 | Temas Dark/Light. Paleta "Dark/Neon Purple" del mockup. |
| 5. CLI shadcn/ui base | `components/ui/` | 📋 | Integrar Button, Input, Card, Dialog, Sheet, etc. |
| 6. Type Safety Pipeline | `lib/api.types.ts` | 📋 | Correr `openapi-typescript`. Garantizar end-to-end con .NET. |
| 7. API Client Typed base | `lib/api-client.ts` | 📋 | Configuración de JWT auth, error intercepting y *Result Pattern*. |
| 8. Zod Schemas Centrales | `lib/schemas.ts` | 📋 | Centralizar reglas de validación (Auth, Ingest, Profile) para evitar repetición. |
| 9. Auth Layer (Zustand + JWT) | `lib/auth.ts` | 📋 | Flujos de Login, Update de Tokens y Logout. |
| 10. SignalR Base Singleton | `lib/signalr.ts` | 📋 | Instanciar el HubClient temprano y atarlo al estado global para chequear conectividad (`Disconnected`, `Reconnecting`). |
| 11. Rutas Públicas de Auth | `app/(public)/*` | 📋 | Páginas de Login (`/login`) y Registro (`/register`). |
| 12. Protección de Rutas | `middleware.ts` | 📋 | Proteger toda ruta bajo el Route Group `(dashboard)`. |

---

## Phase 2 — App Shell (El Layout / Skeleton)

| Tarea | Archivo(s) | Estado | Notas |
|---|---|---|---|
| 0. Test Hooks de Layout | `tests/layout` | 📋 | Validar que el Sidebar colapse según store y se fijen skeletons en carga. |
| 1. Configurar Zustand Store | `store/ui-store.ts` | 📋 | Estado visual (ej: `sidebarOpen`, `consultantPanelOpen`). |
| 2. Sidebar Fijo Fira Code nav | `components/shared/layout/Sidebar.tsx` | 📋 | Integrar Logo y botones con fuentes técnicas. |
| 3. Topbar (ThemeToggle + actions)| `components/shared/layout/Topbar.tsx` | 📋 | Cmd+K Search trigger (vista vacía por ahora), botón "Consultant". |
| 4. Consultant Panel (Slide) | `components/shared/layout/ConsultantPanel.tsx`| 📋 | Ocupará 380px. Estado dependiente de Zustand. Shell vacío por ahora. |
| 5. AppShell Layout general | `app/(dashboard)/layout.tsx` | 📋 | Flex grid combinando Sidebar + Main + Consultant Panel. |
| 6. Responsive UI Handlers | `AppShell` | 📋 | `<768px` pasa Sidebar a Mobile Bottom/Drawer. `<1100px` superpone ConsultantPanel. |
| 7. Dashboard RSC Home | `app/(dashboard)/page.tsx` | 📋 | Título, StatCards y estructura dos columnas del Dashboard. |
| 8. Dashboard Ghost Loaders | `app/(dashboard)/loading.tsx` | 📋 | Skeletons visualmente calcados del dashboard real para feedback veloz. |
| 9. Error Boundary & Suspense | `app/(dashboard)/error.tsx` | 📋 | Limitar impacto de roturas a nivel de hoja, no de toda el App Shell. |

---

## Phase 3 — Core Features (Los 4 Pilares - CRUD y View)

### 3A. Knowledge Brain
| Tarea | Archivo(s) | Estado | Notas |
|---|---|---|---|
| 0. Zod Validations Tests | `tests/features/brain` | 📋 | Unit tests para `ingestKnowledgeAction` garantizando error handling del Action. |
| 1. Tabla de Conocimientos | `features/brain/components/KnowledgeTable.tsx` | 📋 | Uso de Tanstack Query. |
| 2. Ingesta Híbrida (Action) | `features/brain/actions.ts` | 📋 | Server Action `ingestKnowledgeAction` validada con Zod. |
| 3. Buscador Semántico RAG | `features/brain/components/SearchBar.tsx` | 📋 | POST hacia `/knowledge/search`. |

### 3B. Architecture Consultant
| Tarea | Archivo(s) | Estado | Notas |
|---|---|---|---|
| 0. Unit Tests Actions | `tests/features/consultant`| 📋 | Tests del `sendMessageAction` validando Zod y mocks de error. |
| 1. Estructura de Historial | `features/consultant/hooks/useConversations.ts`| 📋 | Fetch listado de conversaciones activas/archivadas. |
| 2. Message Bubbles Render | `features/consultant/components/ChatPanel.tsx` | 📋 | Identidad visual (Fills diferentes User vs AI). Uso de Markdown. |
| 3. Hybrid Chat Input (POST) | `features/consultant/actions.ts` | 📋 | Server Action `sendMessageAction` para inicio de comando al server. |

### 3C. Code Guardian
| Tarea | Archivo(s) | Estado | Notas |
|---|---|---|---|
| 0. Unit Tests Actions | `tests/features/guardian`| 📋 | Validar parseo de URLs de Github al disparar submits. |
| 1. Reporte Visual de Stats | `features/guardian/components/AnalysisReport.tsx`| 📋 | Dashboard individual del Repo. |
| 2. Repositorio SubmitAction | `features/guardian/actions.ts` | 📋 | Server action para enviar un repositorio al escáner. |

### 3D. Trends Radar & 3E. Admin Panel
| Tarea | Archivo(s) | Estado | Notas |
|---|---|---|---|
| 0. Tests Zustand Store Auth| `tests/store` | 📋 | Asegurar que Admin panel quede inaccesible para Roles 'User'. |
| 1. Radar Snapshots | `features/trends/page.tsx` | 📋 | Tabla base de visualización de crecimiento de trends. |
| 2. Admin Dashboards | `features/admin/page.tsx` | 📋 | Tabla de tokens consumidos, approval requests (roles ocultos si en User). |

---

## Phase 4 — Real-Time Layer (El Sistema Nervioso - SignalR)

*(Aclaración: El backend ya provee los 3 hubs, está "Ready". La seguridad faltante del lado del backend no bloquea el progreso).*

| Tarea | Archivo(s) | Estado | Notas |
|---|---|---|---|
| 0. Integrations Test MSW | `tests/signalr` | 📋 | MSW para comprobar updates de estado al entrar eventos mockeados por SignalR. |
| 1. Chat Streaming AI Tokens | `features/consultant/hooks/useConversation.ts` | 📋 | `ReceiveMessageChunk` -> Mutar estado del SignalR, no del React Formulario. |
| 2. Progress Ingest Dashboard | `features/brain/components/IngestProgress.tsx` | 📋 | Recibir porcentaje de extracción y embeddings. |
| 3. Health Analysis Live Log | `features/guardian/` | 📋 | Mostrar fases del escáner a medida que levanta issues. |

---

## Phase 5 — Resiliency & 10/10 Layer (Capa "Premium Architect")

| Tarea | Archivo(s) | Estado | Notas |
|---|---|---|---|
| 0. Stress Test Retry Queue | `tests/resiliency` | 📋 | Simular caída de red y asegurar queue local y rebote `useOptimistic`. |
| 1. Zero-Latency UI (Chat) | `features/consultant/` | 📋 | Integrar `useOptimistic` de React 19 para pintar mensajes User inmediatamente. |
| 2. Zero-Latency Acciones | Varias (EJ: `PublishRequest`)| 📋 | Botones de Accept/Reject reaccionan optimísticamente antes del POST. |
| 3. Offline Queue & Retry | `store/ui-store.ts`, `signalr.ts` | 📋 | Catch network errors, dejar mensajes trabados en "Retry" en vez de descartarlos. |
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
