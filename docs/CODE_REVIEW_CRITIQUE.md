# Code Review — The Sentient Architect
> Rama: `review/code-critique` | Fecha: 2026-04-13 | Reviewer: Senior Architect mode (brutal)

Este documento es una revisión crítica exhaustiva del codebase. Cada issue tiene archivo + línea + por qué importa + cómo arreglarlo.

Clasificación:
- **CRITICAL** — roto, inseguro o produce bugs en producción. Fix inmediato.
- **MAJOR** — deuda técnica significativa. Fix antes de escalar.
- **REGULAR** — subóptimo. Fix antes de release.
- **MINOR** — calidad de código. Fix cuando haya espacio.

---

## Índice

1. [Domain Layer — DDD & C# Moderno](#1-domain-layer--ddd--c-moderno)
2. [Application Layer — Use Cases & Result Pattern](#2-application-layer--use-cases--result-pattern)
3. [Data Layer — EF Core & Configuraciones](#3-data-layer--ef-core--configuraciones)
4. [Infrastructure — DI, SK, Background Jobs](#4-infrastructure--di-sk-background-jobs)
5. [API Layer — Endpoints, Auth, REST](#5-api-layer--endpoints-auth-rest)
6. [Frontend — Next.js, React, TanStack, Zustand](#6-frontend--nextjs-react-tanstack-zustand)
7. [Cross-Cutting — Multi-tenancy, Observabilidad, Tests](#7-cross-cutting--multi-tenancy-observabilidad-tests)
8. [Resumen de Prioridades](#8-resumen-de-prioridades)

---

## 1. Domain Layer — DDD & C# Moderno

### CRITICAL

**1.1 `User.UpdateDisplayName()` tira excepción — viola la regla más básica del Domain**
- `src/SentientArchitect.Domain/Entities/User.cs` línea 29
- `ArgumentException.ThrowIfNullOrWhiteSpace()` está explícitamente prohibido en Domain. ZERO exceptions, ZERO throw. La validación vive en Application via Result pattern.
- Fix: eliminar el throw, retornar `Result.Failure(...)` o ignorar.

**1.2 `BaseEntity` no tiene `UpdatedAt`, ni version, ni domain events**
- `src/SentientArchitect.Domain/Abstractions/BaseEntity.cs`
- En un sistema con agentes AI que modifican estado, sin `UpdatedAt` no podés auditar cuándo cambió un item. Sin `Version` no tenés optimistic concurrency. Sin domain events, transiciones como "KnowledgeItem published", "Analysis completed" se pierden.
- Fix mínimo:
  ```csharp
  public abstract class BaseEntity
  {
      public Guid Id { get; private set; }
      public Guid TenantId { get; protected set; }
      public DateTime CreatedAt { get; private set; }
      public DateTime UpdatedAt { get; private set; }
      private readonly List<DomainEvent> _events = [];
      public IReadOnlyList<DomainEvent> DomainEvents => _events.AsReadOnly();
      protected void RaiseDomainEvent(DomainEvent e) => _events.Add(e);
      public void ClearDomainEvents() => _events.Clear();
  }
  ```

**1.3 Collections mutables expuestas públicamente — invariantes rompibles desde afuera**
- `Conversation.cs` línea 42: `public ICollection<ConversationMessage> Messages`
- `KnowledgeItem.cs` líneas 39-40: colecciones sin protección
- `TechnologyTrend.cs` línea 30-35: `Sources` expuesta como `List<string>`
- `AddSource()` verifica duplicados en línea 52-56, pero cualquier código externo puede hacer `trend.Sources.Add("cualquier cosa")` bypasseando la lógica.
- Fix:
  ```csharp
  private readonly List<ConversationMessage> _messages = [];
  public IReadOnlyList<ConversationMessage> Messages => _messages.AsReadOnly();
  ```

**1.4 Invariantes NO se validan en los constructores**
- `User.cs` líneas 7-13: `Email`, `UserName`, `DisplayName` se asignan sin validar nada.
- `KnowledgeItem.cs` líneas 8-21: acepta `Guid.Empty` como `UserId` y `TenantId` sin objeción.
- Un entity en estado inválido llega a la DB. La validación se repite inconsistentemente en cada use case.

**1.5 Zero domain events — las transiciones de estado son invisibles**
- Ningún entity tiene eventos de dominio. `KnowledgeItem.MarkAsCompleted()`, `Conversation.Archive()`, `AnalysisReport.Complete()` modifican estado sin levantar eventos.
- Consecuencia: para notificar al usuario cuando un análisis completa, tenés que hacer polling o tener lógica de notificación acoplada al use case.
- Fix: base `DomainEvent` record + `RaiseDomainEvent()` en cada transición de estado. Los Application handlers despachan los eventos después de `SaveChangesAsync`.

### MAJOR

**1.6 Primitive obsession — cero Value Objects en todo el domain**
- `Email`, `UserName`, `DisplayName`, `RepositoryUrl`, `SourceUrl` son `string` crudos.
- `UserId`, `TenantId`, `ConversationId` son `Guid` crudos — no tenés garantía de que no confundís un `UserId` con un `TenantId` en una llamada.
- Fix: sealed records con validación integrada:
  ```csharp
  public sealed record Email(string Value)
  {
      public static Result<Email> Create(string v) =>
          v.Contains('@') ? Result<Email>.SuccessWith(new Email(v.Trim().ToLower()))
          : Result<Email>.Failure(["Email inválido"]);
  }
  ```

**1.7 `KnowledgeItemTag` tiene setters públicos — join table sin encapsulación**
- `src/SentientArchitect.Domain/Entities/KnowledgeItemTag.cs`
- `KnowledgeItemId` y `TagId` son `public set`. Cualquier código puede reasignar la FK.
- Fix: `private set` + constructor que valide que ninguno sea `Guid.Empty`.

**1.8 `TenantId` falta en `TechnologyTrend` y `TrendSnapshot`**
- Estos entities no tienen `TenantId`. En un sistema multi-tenant, si los trends son globales debería ser explícito (interface `IGlobalEntity`). Si son por tenant, falta el campo.
- Consecuencia potencial: trends de un tenant visibles para otro.

**1.9 Sin Specification Pattern — query logic en Application**
- No hay `Specification<T>`. Las queries complejas como "conversaciones activas del usuario X en tenant Y" viven en los use cases, no en el domain.
- Consecuencia: duplicación de query logic, imposible testear queries independientemente.

**1.10 Sin aggregate root explícito — boundaries ambiguos**
- No hay `IAggregateRoot` marker interface. No es claro qué entidades son raíz de aggregate vs. child entities.
- `AnalysisFinding` puede crearse independientemente de `AnalysisReport` — roto.
- `ConversationMessage` debería crearse sólo a través de `Conversation.AddMessage()`.

### REGULAR

**1.11 `init` y `required` keywords no usados donde corresponden**
- `CreatedAt` debería ser `init` — solo se setea una vez en construcción.
- Propiedades obligatorias deberían ser `required` para catch en compilación.

**1.12 Pattern matching no usado en lógica de enums**
- Varios lugares chequean enum con `if (status == X)` en lugar de `switch expression` exhaustivo.
- Riesgo: al agregar un nuevo valor de enum, los switch no-exhaustivos fallan silenciosamente.

**1.13 `DateTime.UtcNow` hardcodeado en behavior methods — no testeable**
- `MarkAsProcessing()`, `MarkAsCompleted()`, etc. llaman `DateTime.UtcNow` directamente.
- Hace imposible testear transiciones de estado con fechas controladas.
- Fix: inyectar `IClock` o recibir el timestamp como parámetro.

---

## 2. Application Layer — Use Cases & Result Pattern

### CRITICAL

**2.1 `Result<T>` no tiene métodos de chaining funcional — diseño débil**
- `src/SentientArchitect.Application/Common/Results/Result.cs`
- No existe `.Map()`, `.Bind()`, `.OnSuccess()`, `.OnFailure()`. El código resultante es:
  ```csharp
  var r1 = await UseCase1();
  if (!r1.Succeeded) return r1; // repetido en cada use case
  var r2 = await UseCase2(r1.Data!);
  if (!r2.Succeeded) return r2;
  ```
- Deberías poder encadenar:
  ```csharp
  return await GetUser(id)
      .Bind(user => Validate(user))
      .Bind(user => Save(user))
      .OnSuccess(user => NotifyAsync(user));
  ```
- Fix: agregar `Bind<TNext>`, `Map<TNext>`, `Ensure`, `OnSuccess`, `OnFailure`.

**2.2 `ErrorType` es enum — no hay información estructurada de error**
- Los errores son listas de strings. No podés distinguir "email duplicado" de "usuario no existe" sin parsear el mensaje.
- Fix: discriminated union de errores:
  ```csharp
  public abstract record Error(string Message);
  public record ValidationError(string Message, string? PropertyName = null) : Error(Message);
  public record NotFoundError(string Message, string ResourceType, Guid Id) : Error(Message);
  public record ConflictError(string Message) : Error(Message);
  ```

**2.3 `IApplicationDbContext` expone `DatabaseFacade` — EF leaking al Application layer**
- `src/SentientArchitect.Application/Common/Interfaces/IApplicationDbContext.cs`
- `DatabaseFacade Database { get; }` es una API interna de EF Core. La interface del Application layer no debería saber que el storage es EF.
- Consecuencia: tests del Application layer requieren un EF DbContext, no se puede mockear.
- Fix: quitar `DatabaseFacade`. Si necesitás transacciones, agregar `IDbTransaction BeginTransactionAsync()` a la interface.

**2.4 `SearchKnowledgeUseCase` — N+1 query explícito**
- `src/SentientArchitect.Application/Features/Knowledge/SearchKnowledge/SearchKnowledgeUseCase.cs` líneas 34-53
- Después de obtener los resultados del vector store, ejecuta un query individual por cada resultado:
  ```csharp
  foreach (var vectorResult in vectorResults)
  {
      var item = await db.KnowledgeItems.FirstOrDefaultAsync(k => k.Id == vectorResult.KnowledgeItemId, ct);
  ```
- Para 20 resultados = 20 queries separados a la DB. Con 100 usuarios concurrentes = 2000 queries.
- Fix:
  ```csharp
  var ids = vectorResults.Select(v => v.KnowledgeItemId).ToList();
  var items = await db.KnowledgeItems
      .Where(k => ids.Contains(k.Id))
      .AsNoTracking()
      .ToDictionaryAsync(k => k.Id, ct);
  ```

**2.5 `IngestKnowledgeUseCase` — 7 llamadas a `SaveChangesAsync()` en un loop**
- `src/SentientArchitect.Application/Features/Knowledge/IngestKnowledge/IngestKnowledgeUseCase.cs` líneas 43, 74, 79, 91, 101, 111
- Cada `SaveChangesAsync()` es un round-trip. Si el item 3 falla, los vectores del chunk 1 y 2 ya se guardaron — estado inconsistente.
- Fix: acumular todos los cambios y un único `SaveChangesAsync()` al final, o una transacción explícita.

**2.6 `DeleteConversationUseCase` — retorna Success cuando el entity NO EXISTE**
- Línea 19: `return Result.Success` independientemente de si se encontró la conversación o no.
- REST semántica violada: DELETE de recurso inexistente debería ser 404, no 200.

**2.7 `ReviewPublishRequestUseCase` — `ReviewerUserId` viene del request body**
- Línea 22: el userId del reviewer viene del body HTTP, no del JWT. Un atacante puede firmar como cualquier usuario.
- Fix: inyectar `IUserAccessor` y usar el userId del token.

**2.8 `ReviewPublishRequestUseCase` — `Guid.Empty` hardcodeado como shared tenant**
- Línea 23: `publishRequest.KnowledgeItem!.PublishToShared(Guid.Empty)` — hardcodeado.
- Si `Guid.Empty` tiene semántica especial de "shared", debería ser una constante nombrada `TenantIds.Shared`.

**2.9 `GetConversations` carga TODOS los mensajes para obtener el `.Count`**
- `src/SentientArchitect.Application/Features/Conversations/GetConversations/GetConversationsUseCase.cs` líneas 13-17
- `.Include(c => c.Messages)` carga todos los mensajes — si hay 500 conversaciones con 100 mensajes cada una = 50K mensajes en memoria sólo para contar.
- Fix: `.Select(c => new { c, MessageCount = c.Messages.Count() })` — EF traduce a SQL COUNT, no carga los mensajes.

**2.10 `GetConversationDetail` carga todos los mensajes sin paginación**
- `Include(c => c.Messages)` + `ToListAsync()` — conversación con 10K mensajes = OOM.
- La API contract dice "last N messages". El use case no respeta eso.
- Fix: `.OrderByDescending(m => m.CreatedAt).Take(50)` antes de materializar.

### MAJOR

**2.11 `IEmbeddingService` no expone el modelo que usa**
- `src/SentientArchitect.Application/Common/Interfaces/IEmbeddingService.cs`
- Retorna `float[]` sin indicar dimensionalidad ni modelo. Si alguien cambia el modelo de embeddings a mitad (1536-dim → 768-dim), los vectores en la DB son incompatibles y la búsqueda da resultados incorrectos silenciosamente.
- Fix: retornar `Embedding` record con `Vector`, `ModelName`, `Dimensions`.

**2.12 `IVectorStore` no tiene método de actualización de embeddings**
- Si cambiás el modelo de embeddings, no podés re-embeddear items existentes sin borrar y re-crear todo.
- Fix: agregar `UpdateEmbeddingAsync` y `GetEmbeddingAsync` a la interface.

**2.13 `ITokenService` retorna `string` — debería retornar un Value Object**
- `CreateToken()` retorna string crudo. Sin `TokenSet` record que incluya `AccessToken`, `RefreshToken`, `ExpiresAt`, la firma queda implícita.

**2.14 Validación inconsistente entre use cases — algunos validan, la mayoría no**
- `ExecuteChatUseCase` línea 18 valida. La mayoría de los demás no validan nada.
- `SubmitRepositoryUseCase` valida la URL pero no `UserId` ni `TenantId` (aceptan `Guid.Empty` silenciosamente).

**2.15 Scoring de `GetRepositoryAnalysis` con números mágicos**
- `src/SentientArchitect.Application/Features/Repositories/GetRepositoryAnalysis/GetRepositoryAnalysisUseCase.cs` líneas 72-88
- `25f`, `10f`, `3f`, `0.5f` — penalizaciones sin nombre, sin tests, sin documentación.
- Esto es lógica de negocio que debería vivir en el Domain (`AnalysisReport.CalculateHealthScore()`).

**2.16 `IChatExecutionService` — callback `onToken` sin contrato definido**
- `Func<string, CancellationToken, Task>?` — ¿qué pasa si el callback tira? El loop de streaming no lo captura.
- Fix: envolver el callback en try-catch dentro del loop, o usar `IAsyncEnumerable<ChatToken>`.

### REGULAR

**2.17 Falta de factory methods nombrados en `Result`**
- No existe `Result.NotFound(message)`, `Result.Conflict(message)`, `Result.Forbidden(message)`.
- Todo usa `Result.Failure([message], ErrorType.X)` — verboso y propenso a errores.

**2.18 Null-forgiving operator `!` sin comentarios**
- `result.Data!` en varios lugares sin explicar por qué null es imposible en ese punto.

---

## 3. Data Layer — EF Core & Configuraciones

### CRITICAL

**3.1 `KnowledgeItemTagConfiguration` — falta un lado de la relación**
- `src/SentientArchitect.Data/Configurations/KnowledgeItemTagConfiguration.cs` líneas 9-19
- Solo configura `HasOne(kit => kit.Tag).WithMany(...)`. No configura la relación con `KnowledgeItem`.
- EF no sabe cómo relacionar `KnowledgeItemTag` con `KnowledgeItem` desde este lado. Funciona porque `KnowledgeItemConfiguration` lo configura desde el otro lado, pero es frágil y ambiguo.
- Fix: agregar:
  ```csharp
  builder.HasOne(kit => kit.KnowledgeItem)
      .WithMany(k => k.KnowledgeItemTags)
      .HasForeignKey(kit => kit.KnowledgeItemId)
      .OnDelete(DeleteBehavior.Cascade);
  ```

**3.2 Comportamiento de cascade DELETE inconsistente para el mismo `ApplicationUser`**
- `KnowledgeItemConfiguration` → `OnDelete(DeleteBehavior.Restrict)` (no borra el item si borrás el user)
- `UserProfileConfiguration` → `OnDelete(DeleteBehavior.Cascade)` (borra el profile)
- `ConversationConfiguration` → `OnDelete(DeleteBehavior.Cascade)` (borra las conversaciones)
- Escenario: intentás borrar un user → falla por Restrict en KnowledgeItem → pero el Profile y Conversations YA SE BORRARON porque EF procesa los cascades antes de detectar el Restrict.
- Estado parcialmente borrado e inconsistente.

**3.3 `ApplicationUser` vs `User` domain — violación arquitectónica**
- `src/SentientArchitect.Data/ApplicationContext.cs` línea 48
- El CLAUDE.md dice "User POCO vive en Domain". Pero `ApplicationContext` ignora `Domain.Entities.User` y usa `ApplicationUser : IdentityUser<Guid>`.
- Lógica de negocio del usuario (roles, multi-tenancy) mezclada con Identity.

**3.4 Sin verificación automática de `HasConversion<string>()` en todos los enums**
- Si alguien agrega una propiedad enum sin la conversión, la DB guarda int mientras el código espera string. Las queries silently fallan o retornan resultados incorrectos.
- Fix: integration test que verifique que todos los enum properties en el modelo tienen `.HasConversion<string>()`.

### MAJOR

**3.5 N+1 potencial con tags en `GetKnowledgeItems`**
- `src/SentientArchitect.Application/Features/Knowledge/GetKnowledgeItems/GetKnowledgeItemsUseCase.cs` líneas 40-78
- `.Include(k => k.KnowledgeItemTags).ThenInclude(t => t.Tag)` existe, pero si alguien remueve ese Include, acceder a `t.Tag` dentro del LINQ-to-Objects post-materialización genera N queries de lazy load silenciosamente (si lazy loading está habilitado).
- Comentar la dependencia explícitamente.

**3.6 `Include(k => k.Embeddings)` para solo verificar `.Any()`**
- `GetKnowledgeItemsUseCase.cs` líneas 43 y 76
- Carga TODOS los vectores en memoria (cada vector es 1536 floats = ~6KB) sólo para chequear si existen.
- Para 100 items con embeddings = ~600KB de datos innecesarios por request.
- Fix: `db.KnowledgeEmbeddings.AnyAsync(e => e.KnowledgeItemId == k.Id)` como subquery.

**3.7 Sin converter de UTC en propiedades `DateTime`**
- Ninguna configuración tiene `.HasConversion(v => v.ToUniversalTime(), v => DateTime.SpecifyKind(v, DateTimeKind.Utc))`.
- Si alguien llama `DateTime.Now` en lugar de `DateTime.UtcNow`, la DB guarda hora local. Las comparaciones de fechas se rompen.

**3.8 Sin `HasPrecision` en columnas float/decimal**
- `TechnologyTrendConfiguration.cs` línea 17: `RelevanceScore` sin precisión especificada.
- Distintas DBs tienen defaults distintos — puede haber rounding inconsistente entre entornos.
- Fix: `.HasColumnType("real")` o `.HasPrecision(5, 2)`.

**3.9 Sin índice compuesto en `ConversationMessage (ConversationId, CreatedAt)`**
- `ConversationMessageConfiguration.cs` línea 23: solo índice en `ConversationId`.
- Las queries de "últimos N mensajes de la conversación X" necesitan `ORDER BY CreatedAt` — sin índice compuesto es un full index scan.
- Fix: `builder.HasIndex(m => new { m.ConversationId, m.CreatedAt })`.

**3.10 Sin índice en `AnalysisReport.Status`**
- `AnalysisReportConfiguration.cs` línea 27: solo índice en `RepositoryInfoId`.
- Queries como "reports en estado InProgress" hacen full table scan.

**3.11 Sin `IsRequired()` en algunas FKs**
- `UserProfileConfiguration.cs` línea 28-31: FK a ApplicationUser sin `.IsRequired()`.
- EF puede generar FK nullable en la DB, permitiendo registros huérfanos.

### REGULAR

**3.12 JSONB columns sin límite de tamaño**
- `PreferredStack` y `KnownPatterns` con `.HasColumnType("jsonb")` pero sin validación de tamaño.
- Un usuario puede mandar arrays de millones de items — resource exhaustion.

**3.13 Sin configuración explícita de pool size para Npgsql**
- `src/SentientArchitect.Data.Postgres/DataPostgresServiceExtensions.cs` líneas 19-22
- Default de Npgsql es 25 conexiones por proceso. Bajo carga alta = "The pool is exhausted".
- Fix: `MaxPoolSize` configurable desde `appsettings`.

---

## 4. Infrastructure — DI, SK, Background Jobs

### CRITICAL

**4.1 Agent factories registradas como Singleton con plugins Scoped — memory leak + concurrency bug**
- `src/SentientArchitect.Infrastructure/InfrastructureServiceExtensions.cs` líneas 145-146
- `KnowledgeAgentFactory` y `ConsultantAgentFactory` son **Singleton**, pero sus `CreateKernel()` resuelven plugins (`SearchPlugin`, `IngestPlugin`, `ProfilePlugin`) que son **Scoped** y tienen referencia a `IApplicationDbContext` (también Scoped).
- Cuando el scope HTTP termina, el DbContext se dispone. El Singleton Kernel mantiene la referencia muerta.
- Consecuencias: memory leaks, errores de `"A second operation was started on this context"`, datos stale de requests anteriores.
- Fix: registrar factories como Scoped, o usar `IServiceScopeFactory` para crear un scope nuevo por request.

**4.2 JWT Refresh Token — se puede refrescar indefinidamente**
- `src/SentientArchitect.API/Endpoints/AuthEndpoints.cs` líneas 107-115
- `ValidateLifetime = false` — cualquier token expirado puede refrescarse sin límite.
- Un token robado nunca pierde validez realmente.
- Fix: token rotation real — refresh token en DB, invalidar el anterior después de cada rotación, expiración de refresh token.

**4.3 CORS permite cualquier origen + credentials — CSRF**
- `src/SentientArchitect.API/Program.cs` líneas 33-41
- `SetIsOriginAllowed(_ => true)` + `AllowCredentials()` = cualquier sitio malicioso puede hacer requests autenticados en nombre del usuario.
- Fix: whitelist explícita desde `appsettings`:
  ```csharp
  SetIsOriginAllowed(origin => allowedOrigins.Contains(origin))
  ```

**4.4 Sin rate limiting en endpoints de auth**
- `/auth/login`, `/auth/register` sin límite de requests por IP. OWASP A07:2021.
- Fix: `AspNetCore.RateLimiting` con política por IP en rutas `/auth/*`.

**4.5 Sin timeout en llamadas LLM — thread pool exhaustion**
- `src/SentientArchitect.Infrastructure/Chat/ChatExecutionService.cs` líneas 148, 312
- Si el LLM API cuelga, el request cuelga indefinidamente agotando los threads del pool.
- Fix: `new CancellationTokenSource(TimeSpan.FromMinutes(5)).Token` wrapping.

**4.6 `AnthropicOrchestrator` parsea tool calls con regex — frágil**
- `src/SentientArchitect.Infrastructure/Chat/AnthropicOrchestrator.cs` líneas 10-17
- Si Anthropic cambia mínimamente el formato de respuesta (espacios, wrapping extra, encoding), el parsing falla silenciosamente.
- Fix: usar la SDK de Anthropic para parsear tool calls nativamente, o validar estrictamente + log en fallos.

**4.7 Sin ownership check en `JoinConversation` del SignalR Hub**
- `src/SentientArchitect.API/Hubs/ConversationHub.cs`
- `JoinConversation(string conversationId)` no verifica que la conversación pertenezca al usuario que se conecta. Cualquier usuario autenticado puede joinear el hub group de otra conversación y recibir sus mensajes.
- Fix:
  ```csharp
  var conversation = await db.Conversations
      .AsNoTracking()
      .FirstOrDefaultAsync(c => c.Id == convGuid && c.UserId == userId);
  if (conversation is null) { await Clients.Caller.SendAsync("ReceiveError", "Access denied."); return; }
  ```

### MAJOR

**4.8 Sin `FunctionChoiceBehavior` configurado en los Kernels de SK**
- `KnowledgeAgentFactory.cs` y `ConsultantAgentFactory.cs` crean Kernels sin especificar el comportamiento.
- El LLM puede no llamar plugins cuando debería, o llamarlos cuando no debería.
- Fix: `builder.SetFunctionChoiceBehavior(FunctionChoiceBehavior.Auto(...))` explícito.

**4.9 Sin error handling en métodos de plugins SK**
- `SearchPlugin.cs` líneas 17-79: si `embeddingService.GenerateEmbeddingAsync()` tira (API timeout, key inválida), el agente crashea.
- Fix: try-catch con fallback a keyword search, logging del error.

**4.10 Sin token counting antes de enviar al LLM**
- `ChatExecutionService.cs` líneas 128-138, 265-302: se construyen mensajes de contexto potencialmente grandes sin verificar el token budget.
- Si el contexto excede el límite del modelo, la request falla o se trunca silenciosamente.
- Fix: contar tokens con `ITokenCounter` antes de enviar, trigger compaction si supera threshold.

**4.11 Sin compaction automático de conversación**
- `SummaryPlugin.SaveConversationSummaryAsync()` existe pero nunca se llama automáticamente.
- Las conversaciones crecen indefinidamente hasta que la LLM API rechaza el request.

**4.12 Sin Polly — cero resiliencia en llamadas a LLM**
- LLM APIs tienen rate limits y errores transitorios. Sin retry con backoff exponencial, cualquier fallo temporal pierde el request.
- `TrendScanner.cs` líneas 491-514: silently falla entire batches on any API error.
- Fix: Polly `WaitAndRetryAsync` + `CircuitBreaker` para todas las llamadas LLM.

**4.13 Sin `ValidateOnBuild()` en development**
- `Program.cs` no valida el container DI al startup.
- Errores de lifetime (Singleton con Scoped) se descubren en runtime, no en startup.
- Fix:
  ```csharp
  if (app.Environment.IsDevelopment())
      builder.Host.UseDefaultServiceProvider(o => { o.ValidateOnBuild = true; o.ValidateScopes = true; });
  ```

### REGULAR

**4.14 No hay password reset ni account lockout**
- La API contract menciona "423 Locked" pero el código nunca lockea cuentas en N intentos fallidos.
- No existe endpoint `forgot-password` ni `reset-password`.

**4.15 `TokenService` lee la JWT key en cada llamada**
- Debería parsear `SymmetricSecurityKey` una vez en DI y cachearla, no en cada `CreateToken()`.

**4.16 `IdentitySeeder` no es idempotente — race condition en multi-instance**
- Si dos instancias arrancan simultáneamente, ambas intentan crear roles → duplicate key error.
- Fix: `if (!await roleManager.RoleExistsAsync("Admin")) { ... }`.

**4.17 `IUserService` registrado pero nunca usado**
- `InfrastructureServiceExtensions.cs` líneas 81-84. Dead code DI.

---

## 5. API Layer — Endpoints, Auth, REST

### CRITICAL

**5.1 `GetConversationDetail` — cualquier usuario puede leer conversaciones ajenas**
- `src/SentientArchitect.API/Endpoints/ConversationEndpoints.cs` líneas 51-60
- El endpoint recibe el ID por URL y no filtra por userId en el use case.
- Fix: pasar `userId` del JWT al use case, filtrar `c.Id == id && c.UserId == userId`.

**5.2 `DeleteConversation` — mismo authorization bypass**
- Líneas 75-84 — misma situación. Cualquier usuario borra conversaciones de otro.

**5.3 Sin validación de longitud en `KnowledgeEndpoints` POST**
- `src/SentientArchitect.API/Endpoints/KnowledgeEndpoints.cs` líneas 23-66
- `Content` sin límite. Alguien puede postear 1GB y explotar la memoria en el proceso de embedding.
- Fix: `MaxLength(50000)` o validación explícita, retornar `413 Payload Too Large`.

**5.4 Sin validación de longitud en `ChatEndpoints`**
- Mensaje sin límite → agota token budget intencionalmente, costos LLM descontrolados.
- Fix: máximo 5000 chars por mensaje.

**5.5 `TriggerAnalysis` — fire-and-forget con catch vacío**
- `src/SentientArchitect.API/Endpoints/RepositoryEndpoints.cs` líneas 94-102
- `_ = Task.Run(async () => { ... catch { } })` — excepciones completamente tragadas.
- El usuario recibe 202 pero si falla no llega nada via SignalR.
- Fix: loggear el error Y enviarlo al cliente via `AnalysisHub`.

**5.6 `ResultExtensions` — todos los errores no mapeados van a 400 ValidationProblem**
- `src/SentientArchitect.API/Extensions/ResultExtensions.cs` líneas 49-52
- Errores de negocio (Conflict, Unauthorized, Forbidden) retornan 400 con forma de `ValidationProblem`.
- El cliente no puede distinguir "tu JSON está mal" de "no tenés permiso".

### MAJOR

**5.7 Sin API versioning**
- No hay `v1/`, `v2/` en las rutas ni en headers. Cualquier breaking change rompe todos los clientes.
- Fix: `MapGroup("/api/v1")` mínimamente.

**5.8 Sin OpenAPI/Swagger annotations en endpoints**
- Minimal API sin `.WithName()`, `.WithDescription()`, `.Produces<T>()`, `.ProducesValidationProblem()`.
- La documentación es cero — cualquier consumidor del API tiene que leer el código.

**5.9 Sin health checks**
- No hay `/health/ready` ni `/health/live`.
- En Kubernetes o cualquier orquestador, sin health checks el pod nunca se considera ready.
- Fix: `services.AddHealthChecks().AddNpgsql(connStr)`, mapear `/health/live` y `/health/ready`.

**5.10 Sin correlation ID middleware**
- Los logs de diferentes requests son indistinguibles. Debugging imposible en producción.
- Fix: middleware que genera o propaga `X-Correlation-Id` y lo agrega al log scope.

**5.11 Sin logging de requests/responses**
- No hay request logging. Imposible saber qué endpoints fallan en producción.

**5.12 `SearchKnowledge` — `maxResults` sin bounds**
- `src/SentientArchitect.API/Endpoints/KnowledgeEndpoints.cs`
- Acepta `maxResults=99999` sin restricción. Fix: `Math.Clamp(maxResults, 1, 100)`.

**5.13 `GlobalExceptionHandler` retorna 500 genérico para todo — con TODO comment**
- `src/SentientArchitect.API/Middleware/GlobalExceptionHandler.cs` línea 19
- Hay un `TODO` comentado ahí mismo. DB down, violación de constraint, null reference — todos retornan "Server error" sin diferenciación.

**5.14 `AdminEndpoints.TriggerTrendScan` — mismo fire-and-forget con excepciones tragadas**
- `src/SentientArchitect.API/Endpoints/AdminEndpoints.cs` líneas 50-62. Misma situación que 5.5.

### REGULAR

**5.15 Endpoints sin `Consumes("application/json")`**
- Si un cliente envía XML o form-data, el error es críptico.

**5.16 `ProfileEndpoints` sin validación de listas**
- `PreferredStack` y `KnownPatterns` sin límite de items ni longitud por string.

**5.17 Response de `SearchKnowledge` no incluye timing metrics**
- API contract especifica `queryEmbeddingTimeMs` y `searchTimeMs`. No se calculan ni retornan. Contract mismatch.

**5.18 Sin security headers**
- `X-Frame-Options`, `X-Content-Type-Options`, `Content-Security-Policy` no configurados.
- Fix: `app.UseSecurityHeaders()` con NWebsec o middleware propio.

---

## 6. Frontend — Next.js, React, TanStack, Zustand

### CRITICAL

**6.1 Contraseña de test en source code — eliminar YA**
- `src/app/(dashboard)/guardian/page.tsx` línea 28
- `// password = 12345678!?` — en git history para siempre. Rotar la contraseña si se usó en algún ambiente.

**6.2 JWT en `localStorage` — XSS vulnerable**
- `src/lib/auth.ts` líneas 55+
- Access token Y refresh token en localStorage. XSS = robo total de sesión.
- Fix: cookies `httpOnly`, `Secure`, `SameSite=Strict`. Los Server Actions de Next.js están diseñados para esto.

**6.3 Sin `middleware.ts` — rutas del dashboard sin protección server-side**
- La protección de rutas es sólo client-side. Fácilmente bypaseable.
- Fix: `middleware.ts` en el root que valide `sa_token` cookie y redireccione a `/login`.

**6.4 `useOptimistic` sin rollback en errores de red**
- `src/features/consultant/components/ChatPanel.tsx` líneas 150-153
- Rollback solo si `result?.serverError` es truthy. Network errors, timeouts → mensaje optimista queda visible como fantasma permanente.
- Fix: rollback en cualquier estado que no sea éxito explícito.

**6.5 `useOfflineQueue` — stale closure por dependency array incompleto**
- `src/hooks/useOfflineQueue.ts` línea 53
- `eslint-disable-next-line` sin documentar por qué. `execute`, `dequeueOfflineAction`, `offlineQueue` se usan dentro pero no están en deps → closures stale → comportamiento incorrecto.

**6.6 `offlineQueue` persistida en localStorage sin encriptar**
- `src/store/ui-store.ts` líneas 159-164
- Mensajes del usuario (potencialmente sensibles) en texto plano en disco.

**6.7 Logout no detiene conexiones SignalR**
- `src/lib/auth.ts` líneas 70-78
- `logout()` limpia tokens pero no llama `stopHub()`. El usuario queda técnicamente conectado con estado stale.

### MAJOR

**6.8 Sin `error.tsx` por segmento de ruta**
- `/brain`, `/consultant`, `/guardian`, `/trends` tienen `loading.tsx` pero sin `error.tsx`.
- Cualquier fetch fallido en esas rutas causa un crash sin UI de recovery.
- Fix: agregar `error.tsx` con error recovery para cada segmento.

**6.9 `key={chatKey}` fuerza remount completo del `ChatPanel`**
- `src/app/(dashboard)/consultant/_components/ConsultantView.tsx` línea 54
- Destruye todo el estado interno al cambiar conversación — flicker, pérdida de estado en streaming.
- Fix: `ChatPanel` maneja cambio de `conversationId` con `useEffect` cleanup.

**6.10 Sin `staleTime` en React Query — refetch agresivo**
- `src/features/brain/hooks/useKnowledge.ts` línea 50 y otras
- `staleTime` default = 0 → refetch en cada focus de tab. Los knowledge items no cambian cada segundo.
- Fix: `staleTime: 5 * 60 * 1000` en queries de datos estables.

**6.11 Sin `placeholderData` — UX con pantallas en blanco innecesarias**
- `src/features/consultant/hooks/useConversations.ts`
- Al cambiar entre conversaciones: pantalla en blanco en lugar de datos stale mientras refresca.
- Fix: `placeholderData: (prev) => prev`.

**6.12 `useOptimistic` race condition con query invalidation durante streaming**
- `ChatPanel.tsx` líneas 135-250
- `useOptimistic` depende de que `serverMessages` sea estable. Si una refetch invalida `serverMessages` durante el streaming, `useOptimistic` revierte antes de tiempo.
- Fix: invalidar la query DESPUÉS de que el streaming termine, no durante.

**6.13 Zustand god store — todo en `ui-store.ts`**
- `src/store/ui-store.ts` líneas 41-92
- Un solo store maneja: layout, theme, auth user, hub connections, offline queue, command palette.
- Testing imposible. Fix: separar en `layout-store`, `auth-store`, `connectivity-store`.

**6.14 `api-client.ts` — refresh queue puede quedar stuck si `tryRefresh()` falla con excepción**
- `src/lib/api-client.ts` líneas 102-113
- Si `tryRefresh()` tira, la cola nunca se drena → todos los requests pendientes cuelgan indefinidamente.
- Fix: try-catch + drain explícito de la cola en el error path.

**6.15 Sin virtual scrolling en listas largas**
- `src/features/brain/components/KnowledgeTable.tsx` línea 154
- `src/features/guardian/` — lista de findings
- Con 100+ items: render completo en DOM → jank notable en dispositivos lentos.
- Fix: `@tanstack/react-virtual` o `react-window`.

### REGULAR

**6.16 Auth state (Zustand) no se limpia en 401**
- `src/lib/api-client.ts` líneas 116-120
- Se despacha evento `sa:unauthorized` pero nada lo escucha para limpiar el user del store.

**6.17 Sin React.memo en componentes puros frecuentemente re-renderizados**
- `MessageBubble` en `ChatPanel.tsx` — con 50+ mensajes, re-renderiza todos en cada keystroke del input.
- Fix: `export const MessageBubble = React.memo(...)`.

**6.18 Handlers de `onreconnecting`/`onreconnected` en `useHub` no se limpian**
- `src/hooks/useHub.ts` líneas 51-62
- Si múltiples componentes usan `useHub`, se acumulan handlers en la misma conexión SignalR.

**6.19 Sin type guards — `as X` en lugar de validación**
- `src/features/guardian/hooks/useRepositories.ts` líneas 86-92
- `(raw as RepositorySummary[])` sin validar shape.
- Fix: Zod schema validation en todos los API responses.

**6.20 Server Actions con código duplicado de fetch + error handling**
- `src/features/brain/actions.ts` y otras: cada action reimplementa `fetch + token + error mapping`.
- Fix: `lib/server-client.ts` con fetch tipado centralizado.

**6.21 Sin barrel exports en feature folders**
- `src/features/brain/`, `src/features/consultant/`, etc. sin `index.ts`.
- Imports son rutas internas directas → acoplamiento a estructura interna.

**6.22 Sin `useCallback` en handlers pasados a componentes hijos**
- `src/features/brain/components/SearchBar.tsx` líneas 24-35
- Handlers recreados en cada render → si los hijos están memoizados, la memo se rompe igual.

---

## 7. Cross-Cutting — Multi-tenancy, Observabilidad, Tests

### CRITICAL

**7.1 Multi-tenancy enforcement inconsistente**
- El proyecto tiene `TenantId` en entities pero su aplicación varía drásticamente entre use cases.
- Algunos filtran por `TenantId`, otros no. No hay global query filter en EF Core.
- Fix ideal: `modelBuilder.Entity<KnowledgeItem>().HasQueryFilter(k => k.TenantId == _tenantId)` con `IUserAccessor` inyectado en el `ApplicationContext`.

**7.2 `Guid.Empty` como shared tenant ID — semántica frágil esparcida por todo el codebase**
- Aparece en Domain (`PublishToShared`), Application (ReviewPublishRequest), SearchPlugin.
- Si `Guid.Empty` es el ID del tenant "shared", eso debería ser una constante pública `TenantIds.Shared` en el Domain.

**7.3 Cero tests — la estructura existe pero está vacía**
- `tests/SentientArchitect.UnitTests/` y `tests/SentientArchitect.IntegrationTests/` vacíos.
- Todo lo que se encuentra en este review es potencialmente roto en producción sin saberlo.
- Los bugs críticos listados en este documento (N+1, auth bypass, stale closure) son exactamente lo que los tests deberían haber atrapado.

### MAJOR

**7.4 Sin OpenTelemetry — observabilidad cero en producción**
- No hay distributed tracing. Cuando una request LLM tarda 30s, no podés saber en qué paso.
- Fix: `OpenTelemetry.Extensions.Hosting` + `AddAspNetCoreInstrumentation()` + `AddNpgsql()`.

**7.5 Sin métricas — no hay contadores Prometheus/Grafana**
- Token usage, latencia LLM, hit rate del vector store — no se mide nada.
- En un sistema de AI, monitorear costos de tokens es crítico.

**7.6 Sin structured logging**
- Logs son strings planos, no JSON estructurado. Imposible parsear con Seq/Grafana Loki.
- Fix: `Serilog` con `WriteTo.Console(new CompactJsonFormatter())`.

**7.7 `SaveChangesAsync` no maneja excepciones de DB en use cases**
- Ningún use case captura `DbUpdateException` o `PostgresException`.
- Un constraint violation en la DB explota como 500 con stack trace en lugar de un 409 Conflict con mensaje claro.

---

## 8. Resumen de Prioridades

### Bloque 1 — Security (fix antes de mostrar a alguien)

| Issue | Archivo | Tipo |
|-------|---------|------|
| Contraseña en source code | `guardian/page.tsx:28` | Security |
| JWT en localStorage (XSS) | `lib/auth.ts` | Security |
| Sin middleware.ts de protección | `src/app/` | Security |
| GetConversationDetail sin auth | `ConversationEndpoints.cs:51` | Security |
| DeleteConversation sin auth | `ConversationEndpoints.cs:75` | Security |
| CORS allow all origins + credentials | `Program.cs:33` | Security |
| JWT refresh token sin expiración real | `AuthEndpoints.cs:107` | Security |
| Sin rate limiting en auth | `AuthEndpoints.cs` | Security |
| SignalR JoinConversation sin ownership | `ConversationHub.cs` | Security |
| ReviewerUserId del body, no del JWT | `ReviewPublishRequestUseCase.cs:22` | Security |
| Sin validación de longitud en inputs | `KnowledgeEndpoints.cs`, `ChatEndpoints.cs` | Security/DoS |

### Bloque 2 — Data integrity & Production bugs

| Issue | Archivo | Tipo |
|-------|---------|------|
| Agent factory Singleton con DbContext Scoped | `InfrastructureServiceExtensions.cs:145` | Memory leak |
| N+1 en SearchKnowledge | `SearchKnowledgeUseCase.cs:34` | Performance |
| 7x SaveChangesAsync en loop | `IngestKnowledgeUseCase.cs` | Consistency |
| Cascade DELETE inconsistente para User | `KnowledgeItemConfig` vs `UserProfileConfig` | Data integrity |
| KnowledgeItemTagConfiguration incompleto | `KnowledgeItemTagConfiguration.cs` | EF bug |
| Sin timeout en LLM calls | `ChatExecutionService.cs:148` | DoS |
| useOptimistic race condition | `ChatPanel.tsx:150` | UI bug |
| Refresh queue stuck on error | `api-client.ts:102` | Auth bug |

### Bloque 3 — Architecture debt

| Issue | Impacto |
|-------|---------|
| Cero domain events | Sin eventos, no hay extensibilidad |
| Primitive obsession (sin Value Objects) | Bugs de tipo no detectados |
| Result<T> sin Bind/Map | Código verboso y propenso a errores |
| IApplicationDbContext expone DatabaseFacade | Testabilidad comprometida |
| Sin Specification pattern | Query logic duplicada |
| Zustand god store | Testabilidad imposible |
| Cero tests | Todo lo anterior sin red de seguridad |

### Bloque 4 — Observabilidad & Production-readiness

- Sin OpenTelemetry
- Sin métricas de tokens/costos LLM
- Sin structured logging
- Sin health checks
- Sin correlation ID
- Sin API versioning
- Sin Polly para resiliencia LLM

---

**Total de issues identificados: 85+**

*Review generado en rama `review/code-critique`. NO pushear al repo público.*
