# Plan de Refactoring — The Sentient Architect

> Fecha: 2026-03-31
> Estado: En progreso

## Contexto

Auditoría del código existente reveló patrones funcionales pero desactualizados para un proyecto .NET 9 / C# 13. Este plan prioriza los cambios necesarios para alinear el proyecto con las mejores prácticas modernas (2025-2026).

## Cambios Priorizados

### 🔴 Prioridad Alta (Bugs / Desalineaciones críticas)

#### 1. Fix connection string key mismatch
- **Problema**: `DataPostgresServiceExtensions.cs` busca `"Postgres"`, pero `appsettings.json` tiene `"DefaultConnection"`
- **Solución**: Cambiar `appsettings.json` y `appsettings.Development.json` para usar key `"Postgres"`
- **Archivos**: `appsettings.json`, `appsettings.Development.json`

#### 2. Agregar `.HasConversion<string>()` a todos los enums en EF Core
- **Problema**: 6 enums configurados sin conversión a string — se guardan como int
- **Solución**: Agregar `.HasConversion<string>().HasMaxLength(50)` en cada configuración
- **Archivos**: Todas las configurations en `Data/Configurations/`
- **Enums afectados**: KnowledgeItemType, ProcessingStatus, TagCategory, SuggestionStatus, PublishRequestStatus, QuotaAction

#### 3. Alinear CLAUDE.md — interfaces en Application (no Domain)
- **Problema**: CLAUDE.md dice "Interfaces in Domain" pero el código las tiene en Application
- **Solución**: Actualizar CLAUDE.md y reglas para reflejar la decisión tomada
- **Archivos**: `CLAUDE.md`, `.claude/rules/clean-architecture.md`

### 🟡 Prioridad Media (Modernización de patrones)

#### 4. Entidades con private/init setters + factory methods
- **Problema**: Todas las entidades tienen public setters — no protegen su estado
- **Solución**: Cambiar a `private set` o `init`, agregar constructor privado para EF Core, factory method estático `Create()`
- **Archivos**: Todas las entities en `Domain/Entities/`

#### 5. BaseEntity abstracta con campos de auditoría
- **Problema**: `Id`, `CreatedAtUtc`, `UserId`, `TenantId` se repiten en cada entidad
- **Solución**: Crear `BaseEntity : IEntity` con campos comunes. Entidades heredan de `BaseEntity`
- **Archivos**: `Domain/Abstractions/BaseEntity.cs`, todas las entities

#### 6. Agregar métodos de comportamiento a entidades
- **Problema**: Entidades son data holders puros — no tienen lógica de dominio
- **Solución**: Agregar métodos como `MarkAsCompleted()`, `Approve()`, `Reject()`, `UpdateField()` donde corresponda
- **Archivos**: `ContentPublishRequest.cs`, `ProfileUpdateSuggestion.cs`, `KnowledgeItem.cs`, `TokenUsageTracker.cs`

#### 7. Value Objects para PreferredStack y KnownPatterns
- **Problema**: `List<string>` mutable sin validación — Primitive Obsession
- **Solución**: Crear Value Objects (`TechStack`, `PatternList`) que validen, normalicen y den comportamiento
- **Archivos**: `Domain/ValueObjects/TechStack.cs`, `Domain/ValueObjects/PatternList.cs`, `UserProfile.cs`

#### 8. IVectorStore — agregar userId para scope personal + shared
- **Problema**: `SearchSimilarAsync` solo tiene `tenantFilter`, falta `userId` para filtrar scope personal
- **Solución**: Agregar parámetro `userId` según la firma definida en `vector-db.md`
- **Archivos**: `Application/Common/Interfaces/IVectorStore.cs`

### 🟢 Prioridad Baja (Mejoras futuras)

#### 9. Evaluar ErrorOr vs Result casero
- **Problema**: Result pattern actual tiene constructor parameterless, `List<string>` mutable
- **Solución**: Evaluar migración a ErrorOr (amantinband) o mejorar el Result existente
- **Estado**: Pendiente de decisión

#### 10. Vertical Slice en Application layer
- **Problema**: Application layer todavía no tiene use cases organizados
- **Solución**: Cuando se implementen features, organizar por vertical slice (Features/KnowledgeIngestion/Command, Handler, Validator)
- **Estado**: Se aplica cuando se empiece fase de implementación

#### 11. Switch expressions en lógica de dominio
- **Problema**: Switch statements verbosos (ej: UserProfile field update)
- **Solución**: Reemplazar con switch expressions + pattern matching donde corresponda
- **Estado**: Se aplica junto con punto 6 (métodos de comportamiento)

## Decisiones Tomadas

| Decisión | Opción elegida | Alternativa descartada | Razón |
|----------|---------------|----------------------|-------|
| Ubicación de interfaces | Application/Common/Interfaces/ | Domain/Interfaces/ | Application es el consumidor; alineado con Jason Taylor y Amichai Mantinband |
| Enum storage | String via `.HasConversion<string>()` | Int (default) | Legibilidad, seguridad ante reordenamiento |
| Base entity | Abstract class + IEntity interface | Solo interface | Evita duplicación de Id, audit fields, tenant fields |
| Repository pattern | Mantener interfaces específicas (no genérico) | Generic IRepository<T> | Las interfaces actuales son contratos de dominio, no CRUD genérico |
| string[] collections | Value Objects | Mantener List<string> | Validación, normalización, comportamiento encapsulado |

## Orden de Ejecución

1. ✅ Fix connection string key
2. ✅ Enum string conversions
3. ✅ Alinear docs (CLAUDE.md, clean-architecture.md)
4. ✅ BaseEntity abstracta
5. ✅ Private setters + behavior methods en entidades
6. ⬜ Value Objects (TechStack, PatternList)
7. ✅ Métodos de comportamiento en entidades
8. ✅ IVectorStore scope fix (userId + tenantId + includeShared)
9. ⬜ Evaluar ErrorOr
10. ⬜ Vertical slices (cuando arranque implementación)

---

## Plan Paso a Paso — ChatEndpoints (Prioridad Alta)

Objetivo: reducir complejidad de `ChatEndpoints` sin romper comportamiento actual (streaming + RAG deterministico + consultant loop).

### Etapa 1 — Separar orchestration por capa (ejecutable)

Objetivo: sacar logica de chat del endpoint sin violar Clean Architecture.

Ownership por capa:
- Application: define contratos y caso de uso de coordinacion (sin Semantic Kernel concreto).
- Infrastructure: implementa adaptadores provider-specific (SK/Anthropic, plugins, streaming).
- API: solo transporte HTTP + auth + binding + mapping de Result a HTTP.

Guardrails de capa (obligatorio):
- Application NO referencia tipos de SDK de IA (por ejemplo `ChatHistory`, `ChatMessageContent`, `FunctionCallContent`) ni tipos de SignalR.
- Application trabaja solo con contratos y DTOs propios.
- Cualquier parseo provider-specific (por ejemplo bloques `<invoke>`) vive en Infrastructure.

Tareas:
1. Application — crear contrato de caso de uso de chat (no "orchestrator" en Infrastructure como centro):
	- `src/SentientArchitect.Application/Common/Interfaces/IChatExecutionService.cs`
	- Firma sugerida: `Task<Result<ChatExecutionResponse>> ExecuteAsync(ChatExecutionRequest request, CancellationToken ct = default)`
2. Application — crear DTOs de entrada/salida del caso de uso:
	- `src/SentientArchitect.Application/Features/Chat/ChatExecutionRequest.cs`
	- `src/SentientArchitect.Application/Features/Chat/ChatExecutionResponse.cs`
3. Application — crear caso de uso coordinador (regla de negocio de flujo):
	- `src/SentientArchitect.Application/Features/Chat/ExecuteChatUseCase.cs`
	- Decide ruta Knowledge vs Consultant, valida precondiciones y delega en interfaces.
4. Application — declarar puertos para capacidades externas:
	- `IChatHistoryBuilder`
	- `IKnowledgeResponseGenerator`
	- `IConsultantResponseGenerator`
	- `IConversationStreamPublisher`
5. Infrastructure — implementar adaptadores concretos (Semantic Kernel/SignalR):
	- `src/SentientArchitect.Infrastructure/Chat/KnowledgeResponseGenerator.cs`
	- `src/SentientArchitect.Infrastructure/Chat/ConsultantResponseGenerator.cs`
	- `src/SentientArchitect.Infrastructure/Chat/ConversationStreamPublisher.cs`
6. Infrastructure — encapsular el hot-fix del agentic loop manual en servicio dedicado:
	- `src/SentientArchitect.Infrastructure/Chat/AnthropicOrchestrator.cs`
	- Este servicio es el unico lugar habilitado para `while` + parseo `<invoke>` como workaround del bridge.
7. Application — propagar `Result<T>` en el flujo interno completo (sin retornos directos/throws de control):
	- `ExecuteChatUseCase` devuelve `Result<ChatExecutionResponse>`
	- errores de generacion/stream/persistencia se traducen a `Result.Failure(...)`
8. Infrastructure — registrar DI de implementaciones anteriores en:
	- `src/SentientArchitect.Infrastructure/InfrastructureServiceExtensions.cs`
9. API — reducir `ChatEndpoints` a controlador de transporte:
	- map request HTTP -> `ChatExecutionRequest`
	- invocar `ExecuteChatUseCase`
	- map `Result` -> HTTP (via ResultExtensions)
	- cero logica de prompting/tool-calling en endpoint

Definition of Done Etapa 1:
- `ChatEndpoints` no contiene logica de generacion de respuesta ni loop de tools.
- Toda decision de flujo Knowledge/Consultant vive en Application.
- Todo detalle SK/Anthropic/SignalR vive en Infrastructure.
- No existe ninguna referencia a tipos SK/SignalR dentro de Application.
- El workaround de tool-calling Anthropic queda aislado en `AnthropicOrchestrator`.
- Endpoint transforma `Result` a HTTP sin `try/catch` de negocio.

### Etapa 2 — Extraer historial y streaming como servicios de borde

Objetivo: eliminar duplicacion y acoplamiento tecnico dentro del flujo de chat.

Tareas:
1. Application — contrato de armado de historial (independiente de SK):
	- `src/SentientArchitect.Application/Common/Interfaces/IChatHistoryBuilder.cs`
2. Infrastructure — implementacion de mapeo `ConversationMessage -> ChatHistory`:
	- `src/SentientArchitect.Infrastructure/Chat/SkChatHistoryBuilder.cs`
3. Application — contrato de publicacion de streaming:
	- `src/SentientArchitect.Application/Common/Interfaces/IConversationStreamPublisher.cs`
4. Infrastructure — implementacion de eventos SignalR:
	- `src/SentientArchitect.Infrastructure/Chat/SignalRConversationStreamPublisher.cs`
	- centralizar `ReceiveToken`, `ReceiveComplete`, `ReceiveError` y manejo de excepciones de envio.
5. Application — usar ambos contratos en `ExecuteChatUseCase` para un flujo unico:
	- cargar historial
	- generar respuesta (Knowledge o Consultant)
	- publicar stream
	- devolver resultado final para persistencia
6. API — endpoint queda con 3 responsabilidades:
	- validar request
	- invocar caso de uso
	- persistir mensaje final y responder HTTP
7. Application/API — estandarizar errores y exito con `Result<T>`:
	- evitar retornos ad-hoc en ramas internas
	- un solo punto de mapeo a status code en endpoint

Definition of Done Etapa 2:
- No hay transformaciones `ConversationMessage -> ChatHistory` dentro de `ChatEndpoints`.
- No hay llamadas directas a `IHubContext` desde `ChatEndpoints`.
- El contrato de streaming es testeable por interface en Unit Tests.
- Todos los caminos de error del caso de uso retornan `Result.Failure` consistente.

### Etapa 3 — Endurecer confiabilidad
1. Verificar cobertura de `Result`/`Result<T>` en todos los adaptadores y en el caso de uso (sin excepciones de control).
2. Agregar telemetry estructurada minima por etapa (load history, retrieve context, generation, persist reply).
3. Agregar tests de regresion para:
	- flujo Knowledge con contexto recuperado
	- flujo Consultant con function calling
	- emision ReceiveToken/ReceiveComplete/ReceiveError

### Etapa 4 — Limpieza final
1. Eliminar utilidades/mapeos que queden huérfanos en endpoint.
2. Documentar la arquitectura final del chat flow en `docs/ARCHITECTURE_DECISIONS.md`.
3. Incluir plan de hardening de busqueda textual de `SearchPlugin` usando `pg_trgm` (migracion + indice + benchmark).

---

## Solucion Senior Recomendada — SearchPlugin Escalable (No Implementar Aun)

Problema actual: keyword fallback usa `ToListAsync` y filtra en memoria.

Enfoque recomendado:
1. Mantener vector search como primer paso.
2. Mover fallback textual al motor SQL:
	- Opcion A: PostgreSQL Full Text Search (`tsvector` + `to_tsquery`/`plainto_tsquery`).
	- Opcion B: trigram index (`pg_trgm`) + `ILIKE`/similarity para tolerancia tipografica.
3. Limitar por scope en query SQL (`UserId` / `TenantId`) antes del ranking textual.
4. Unificar ranking final (vector score + text score) con umbral configurable.
5. Indexar columnas usadas (`Title`, `OriginalContent`) y medir p95/p99 antes/despues.

Resultado esperado: misma calidad funcional con mucho menor costo de memoria/CPU en crecimiento.
