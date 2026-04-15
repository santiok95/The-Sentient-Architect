# Production Readiness Plan — The Sentient Architect

> Última actualización: 2026-04-15 | Audit exhaustivo del estado real del código.
> Orden de ejecución: seguridad crítica → correctitud funcional → deuda de dominio → features incompletas → escalabilidad → observabilidad.

---

## Estado del audit — resumen ejecutivo

| # | Ítem | Estado real | Severidad |
|---|------|-------------|-----------|
| 0.1 | Ownership check en `DeleteKnowledgeItem` | ❌ NO IMPL | **CRÍTICO** |
| 0.2 | Ownership check en Hub methods de SignalR | ❌ NO IMPL | **CRÍTICO** |
| 0.3 | Startup validation para `NullEmbeddingService` | ❌ NO IMPL | **CRÍTICO** |
| 0.4 | Refresh token rotation | ❌ NO IMPL | **ALTA** |
| 0.5 | Sanitización contra prompt injection | ⚠️ PARCIAL | **ALTA** |
| 1.1 | Compactación automática de conversaciones | ⚠️ PARCIAL | **MEDIA** |
| 1.2 | `TokenUsageTracker` con incrementos reales | ❌ NO IMPL | **MEDIA** |
| 1.3 | Resolver identidad dual `Domain.User` vs `ApplicationUser` | ⚠️ DEUDA | **MEDIA** |
| 1.4 | Cleanup con `try/finally` en `CodeAnalyzer` | ⚠️ PARCIAL | **BAJA** |
| 2.1 | `User.UpdateDisplayName()` viola zero-throw | ❌ VIOLA | **BAJA** |
| 2.2 | `BaseEntity` sin `UpdatedAt` | ❌ NO IMPL | **MEDIA** |
| 2.3 | Colecciones de navegación expuestas como `ICollection` | ❌ VIOLA | **BAJA** |
| 2.4 | `KnowledgeItemTag` FKs con setters públicos | ❌ VIOLA | **BAJA** |
| 2.5 | `TechnologyTrend` sin `TenantId` | ❌ NO IMPL | **MEDIA** |
| ✅ | `DeleteConversation` / `ArchiveConversation` — ownership | IMPL | — |
| ✅ | `GetConversations`, `GetKnowledgeItems`, `GetRepositories` — filtro por UserId | IMPL | — |
| ✅ | `NullEmbeddingService` registrado condicionalmente | IMPL | — |
| ✅ | `TrendScannerService` — 5 fuentes reales (GitHub, HN, Dev.to, Medium, Releases) | IMPL | — |

---

## BLOQUE 0 — Seguridad crítica

> No negociable. Fixes pequeños, impacto máximo.

### 0.1 Ownership check en `DeleteKnowledgeItem` · CRÍTICO

**Estado real:** El use case recibe solo `Guid KnowledgeItemId` sin `UserId`. Cualquier usuario autenticado puede borrar items ajenos.

**Archivos:**
- `Application/Features/Knowledge/DeleteKnowledgeItem/DeleteKnowledgeItemUseCase.cs`
- `API/Endpoints/KnowledgeEndpoints.cs`

**Fix:**
- Agregar `UserId` a `DeleteKnowledgeItemRequest`
- Validar `item.UserId == request.UserId` antes de `Remove()`
- Retornar `Result.Failure(..., ErrorType.Forbidden)` si no coincide
- Pasar `UserId` desde el endpoint via `IUserAccessor`

---

### 0.2 Ownership check en Hub methods de SignalR · CRÍTICO

**Estado real:** Los hubs solo tienen `[Authorize]`. Cualquier usuario autenticado puede hacer `AddToGroupAsync` con el ID de una conversación o repositorio ajeno y escuchar updates en tiempo real de recursos que no le pertenecen.

**Archivos:**
- `API/Hubs/ConversationHub.cs`
- `API/Hubs/AnalysisHub.cs`
- `API/Hubs/IngestionHub.cs`

**Fix:**
- `ConversationHub.JoinConversation`: verificar en DB que `conversation.UserId == userId del JWT` antes de `AddToGroupAsync`
- `AnalysisHub.JoinRepository`: ídem para `repositoryInfo.UserId`
- `IngestionHub`: ídem
- Si no coincide: no agregar al grupo, retornar error via `Clients.Caller`

---

### 0.3 Startup validation para `NullEmbeddingService` · CRÍTICO

**Estado real:** Si `AI:OpenAI:ApiKey` está vacía, `NullEmbeddingService` se registra silenciosamente. La app arranca sin errores y explota en runtime con `InvalidOperationException` cuando se intenta usar embeddings.

**Archivos:**
- `Infrastructure/InfrastructureServiceExtensions.cs`
- `API/Program.cs`

**Fix:**
- Cuando se registra `NullEmbeddingService`, loggear `LogCritical("OpenAI API key not configured — embedding service is disabled")`
- Si `ASPNETCORE_ENVIRONMENT == Production`, fallar en startup antes de aceptar requests

---

### 0.4 Refresh token rotation · ALTA

**Estado real:** El JWT de 7 días se reutiliza como refresh token (stateless, sin invalidación). Token robado = 7 días de acceso garantizado sin forma de revocarlo.

**Archivos:**
- `Domain/Entities/` — nueva entidad `RefreshToken`
- `Infrastructure/Identity/TokenService.cs`
- `API/Endpoints/AuthEndpoints.cs`
- Nueva migración EF Core

**Fix:**
- Generar `RefreshToken` opaque separado, almacenado en DB con hash
- Al hacer `/auth/refresh`: invalidar el token anterior, emitir uno nuevo (rotation)
- Al hacer `/auth/logout`: invalidar el refresh token activo
- Impacto: moderado — toca el auth flow completo

---

### 0.5 Sanitización mínima contra prompt injection · ALTA

**Estado real:** `SearchPlugin` concatena `query` y `k.OriginalContent` directamente al prompt del LLM sin ningún escape. Prompt injection viable desde el input del usuario o desde un knowledge item malicioso.

**Archivos:**
- `Infrastructure/Agents/Knowledge/SearchPlugin.cs`
- `Application/Features/Knowledge/IngestKnowledge/IngestKnowledgeUseCase.cs`

**Fix:**
- Al ingestar: strip de patrones conocidos (`Ignore all previous`, `[INST]`, `###`, `<|system|>`, `<|user|>`)
- En `SearchPlugin`: truncar el contenido de cada chunk a máximo razonable (ej: 800 chars) antes de concatenar
- Loggear cuando score de similitud es anormalmente alto para un query corto (posible adversarial input)

---

## BLOQUE 1 — Correctitud funcional

> El sistema tiene features que parecen funcionar pero no funcionan.

### 1.1 Compactación automática de conversaciones · MEDIA

**Estado real:** `SummaryPlugin.CompactConversation()` existe pero nada lo invoca. El agente podría usarlo si quisiera, pero no hay lógica automática. Conversaciones largas van a exceder el context window silenciosamente.

**Archivos:**
- `Application/Features/Conversations/Chat/ExecuteChatUseCase.cs`
- `appsettings.json`

**Fix:**
- En `ExecuteChatUseCase`, antes de invocar al agente: contar mensajes
- Si `messages.Count > threshold` (configurable via `Conversation:CompactionThreshold`), invocar `SummaryPlugin.CompactConversation()` y reemplazar el historial con el resumen

---

### 1.2 `TokenUsageTracker` sin incrementos · MEDIA

**Estado real:** La entidad `TokenUsageTracker` existe con `ConsumeTokens()`, está en `IApplicationDbContext`, pero **ningún flujo LLM lo llama**. La quota feature es una ilusión — nunca se aplica.

**Archivos:**
- `Infrastructure/Chat/ChatExecutionService.cs`
- `Application/Features/Conversations/Chat/ExecuteChatUseCase.cs`

**Fix:**
- En `ChatExecutionService`, después de cada invocación al agente, leer tokens usados de `response.Metadata`
- Llamar `tracker.ConsumeTokens(tokensUsed)` y persistir
- Antes de invocar: si quota excedida, retornar `Result.Failure` con mensaje claro

---

### 1.3 Resolver identidad dual `Domain.User` vs `ApplicationUser` · MEDIA

**Estado real:** `Domain.User` POCO existe en domain. `ApplicationContext` lo ignora con `builder.Ignore<User>()` (comentario en el código reconoce el problema). Garantiza confusión en cualquier dev nuevo.

**Archivos:**
- `Domain/Entities/User.cs`
- `Data/ApplicationContext.cs`

**Decisión recomendada:** Eliminar `Domain.User`. `ApplicationUser` en Data es la fuente de verdad. Documentar como ADR-003 en `ARCHITECTURE_DECISIONS.md`.

---

### 1.4 Cleanup con `try/finally` en `CodeAnalyzer` · BAJA

**Estado real:** El cleanup del directorio temporal existe en el happy path y en el catch final, pero no en un `finally` garantizado. Si el análisis explota en ciertos puntos intermedios, el directorio queda huérfano en disco.

**Archivo:** `Infrastructure/Guardian/CodeAnalyzer.cs`

**Fix:** Envolver el bloque completo de análisis en `try/finally`. El `finally` siempre borra `clonePath` independientemente del resultado.

---

## BLOQUE 2 — Domain hygiene

> Violaciones de las convenciones propias del proyecto.

### 2.1 `User.UpdateDisplayName()` viola zero-throw · BAJA

**Estado real:** `ArgumentException.ThrowIfNullOrWhiteSpace(name)` en línea 29 de `Domain/Entities/User.cs`. Prohibido por convención: ZERO exceptions en Domain.

**Fix:** Guard silencioso — si `name` es vacío o whitespace, no actualizar y retornar. La validación del input vive en Application.

---

### 2.2 `BaseEntity` sin `UpdatedAt` · MEDIA

**Estado real:** `BaseEntity` solo tiene `Id` y `CreatedAt`. Sin `UpdatedAt` no podés auditar cuándo cambió un entity en producción. Algunas entidades lo tienen manualmente (ej: `Conversation`) pero sin consistencia.

**Archivos:**
- `Domain/Abstractions/BaseEntity.cs`
- `Data/ApplicationContext.cs` — override de `SaveChangesAsync`
- Nueva migración

**Fix:**
```csharp
public abstract class BaseEntity : IEntity
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;
}
```
Actualizar `UpdatedAt` automáticamente en el override de `SaveChangesAsync`.

---

### 2.3 Colecciones de navegación expuestas como `ICollection` · BAJA

**Estado real:** `Messages`, `Embeddings`, `Findings`, `Reports`, `Snapshots` son `ICollection<T>` público con setter privado. Permite `.Add()` desde fuera bypasseando la lógica de dominio, ya que `ICollection` expone `Add()`.

**Entidades:** `Conversation`, `KnowledgeItem`, `AnalysisReport`, `RepositoryInfo`, `TechnologyTrend`

**Fix:**
```csharp
private readonly List<ConversationMessage> _messages = [];
public IReadOnlyList<ConversationMessage> Messages => _messages.AsReadOnly();
public void AddMessage(ConversationMessage msg) => _messages.Add(msg);
```

---

### 2.4 `KnowledgeItemTag` FKs con setters públicos · BAJA

**Estado real:** `KnowledgeItemId` y `TagId` son `public set` — cualquier código puede reasignar las FKs de la join table.

**Archivo:** `Domain/Entities/KnowledgeItemTag.cs`

**Fix:** `private set` + constructor que valide que ninguno sea `Guid.Empty`.

---

### 2.5 `TechnologyTrend` sin `TenantId` · MEDIA

**Estado real:** `TechnologyTrend` y `TrendSnapshot` no tienen `TenantId`, a diferencia de `KnowledgeItem`, `Conversation`, `RepositoryInfo`. Inconsistencia en el modelo multi-tenant — todos los usuarios ven todos los trends.

**Decisión requerida:**
- **Opción A (recomendada):** Trends son globales → agregar interface `IGlobalEntity` para documentarlo explícitamente. Sin `TenantId`.
- **Opción B:** Trends son por tenant → agregar `TenantId` + migración + filtros en queries.

---

## BLOQUE 3 — Features incompletas

> Con impacto directo en usuarios reales.

- [ ] **Code Guardian — implementar `DependencyPlugin`**
  Escaneo de NuGet packages contra vulnerabilidades conocidas (OSS Index o NVD). El `CodeAnalyzer` con Roslyn ya está implementado y funcionando — esto es el siguiente paso del pilar Guardian.

- [ ] **Trends Radar — implementar relevance scoring contra `UserProfile`**
  `TechnologyTrend.RelevanceScore` existe pero nada lo calcula. El scoring debe comparar el trend contra `PreferredStack` y `KnownPatterns` del perfil del usuario. Las 5 fuentes de scraping ya están implementadas.

- [ ] **`ArchitectureRecommendation` — conectar al flujo del Consultant Agent**
  La entidad y el endpoint `GET /conversations/{id}/recommendations` existen, pero el Consultant Agent nunca genera ni persiste recomendaciones estructuradas. Definir cuándo el agente debe emitir una `ArchitectureRecommendation` vs una respuesta libre.

- [ ] **Frontend — agregar estados de error explícitos en todas las vistas**
  Loading y empty states existen; los error states son inconsistentes. Un 500 del backend no debe dejar la UI congelada. Agregar `error.tsx` o manejo de error state en cada feature que hace fetching.

---

## BLOQUE 4 — Deuda arquitectónica

- [ ] **Reemplazar keyword fallback en memoria en `SearchPlugin`**
  `ToListAsync()` + `Where()` en memoria trae TODOS los items del usuario antes de filtrar. Con miles de items: OOM o latencia inaceptable. Implementar con PostgreSQL FTS (`tsvector` + `tsquery`) o trigram index (`pg_trgm` + GIN).

- [ ] **Mover validación de input de endpoints a capa Application**
  `KnowledgeEndpoints.cs` hace validación inline. Lógica de negocio en presentación. Crear validators en Application y llamarlos desde los use cases.

- [ ] **Refactor de `ChatEndpoints` según `REFACTORING_PLAN.md`**
  Ya documentado como alta prioridad. Ejecutar por etapas para reducir riesgo de regresión.

- [ ] **Propagar `CancellationToken` en todos los hub methods**
  Desconexión abrupta → operaciones EF Core siguen corriendo. Agregar `CancellationToken ct` a cada método de hub y propagarlo.

- [ ] **Definir estrategia de tracing de decisiones del agente**
  No hay logging de qué plugin fue invocado, con qué args, qué retornó, cuánto tardó. Mínimo: log estructurado con `correlationId`, `pluginName`, `functionName`, `durationMs`, `resultLength`.

---

## BLOQUE 5 — Tests

- [ ] **Tests de ownership y authorization** — Cubrir: DELETE de item ajeno, JOIN a conversación ajena en SignalR. Bugs silenciosos sin tests dedicados.
- [ ] **Tests de integración para auth flows** — `Microsoft.AspNetCore.Mvc.Testing` + `Testcontainers`.
- [ ] **Tests para `ConversationHub`** — join/leave, streaming, desconexión abrupta, acceso a conversación ajena.
- [ ] **Tests para `SearchPlugin`** — threshold filtra correctamente, keyword fallback no cruza users, falla gracefully sin embedding service.
- [ ] **Tests de componentes frontend** — `ChatPanel`, `ConversationList`, `RepoPicker`. Vitest + Testing Library.

---

## BLOQUE 6 — Escalabilidad y observabilidad

- [ ] **Rate limiting por usuario en endpoints LLM** — `POST /conversations/{id}/messages` sin límite por usuario. Agregar política con `System.Threading.RateLimiting`.
- [ ] **Redis backplane para SignalR** — Una línea de código, pero requiere planificación operacional. Definir threshold de activación.
- [ ] **Métricas de LLM por invocación** — `durationMs`, `tokensInput`, `tokensOutput`, `modelUsed`, `pluginName`. Necesario para controlar costos.
- [ ] **Health check para providers de AI** — El readiness check solo verifica PostgreSQL. Si Anthropic u OpenAI están caídos, la app arranca y falla en runtime.
- [ ] **`OutputCache` en endpoints de solo lectura** — `GET /trends`, `GET /repositories/{id}/analysis`. Reduce carga en DB sin Redis.
- [ ] **Estrategia de backup de `knowledge_embeddings`** — Regenerables pero costosos. Documentar frecuencia, retención y restore.

---

## Orden de ejecución

```
Fase 1 — Seguridad crítica (esta semana)
  ├── 0.1 Ownership check en DeleteKnowledgeItem     ← 30 min
  ├── 0.2 Ownership check en Hub methods             ← 1h
  └── 0.3 Startup validation NullEmbeddingService    ← 30 min

Fase 2 — Seguridad alta (próxima semana)
  ├── 0.4 Refresh token rotation                     ← medio día
  └── 0.5 Sanitización prompt injection              ← 2h

Fase 3 — Correctitud funcional
  ├── 1.1 Compactación automática de conversaciones
  ├── 1.2 TokenUsageTracker con incrementos reales
  └── 1.3 Eliminar Domain.User (ADR-003)

Fase 4 — Domain hygiene (sin impacto en runtime)
  ├── 1.4 try/finally en CodeAnalyzer
  ├── 2.1 User.UpdateDisplayName zero-throw
  ├── 2.2 BaseEntity.UpdatedAt + migración
  ├── 2.3 Colecciones IReadOnlyList
  ├── 2.4 KnowledgeItemTag private set
  └── 2.5 Decisión TechnologyTrend TenantId

Fase 5+ — Features, tests, escalabilidad (según prioridad de negocio)
```
