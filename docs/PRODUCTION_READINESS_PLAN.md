# Production Readiness Plan — The Sentient Architect

Orden de ejecución: seguridad → correctitud funcional → deuda arquitectónica → features incompletas → escalabilidad → calidad.

---

## BLOQUE 0 — Seguridad

> No negociable antes de cualquier deploy.

- [ ] **Validar ownership en cada hub method de SignalR**
  Actualmente solo `JoinConversation` valida el `UserId`. Cada método de `ConversationHub`, `AnalysisHub` e `IngestionHub` debe verificar que el `UserId` del JWT coincide con el recurso solicitado antes de operar.

- [ ] **Ownership check en `DeleteKnowledgeItem`**
  El use case recibe solo `id`, sin `userId`. Cualquier usuario autenticado puede borrar items ajenos. Agregar `UserId` al request y validar en el use case.

- [ ] **Ownership check en todos los use cases de delete/archive**
  Auditar `DeleteConversation`, `ArchiveConversation`, `DeleteKnowledgeItem`, `DeleteRepository`: todos deben validar `UserId` antes de operar. Algunos ya tienen el fix post-review — confirmar que todos están cubiertos.

- [ ] **Sanitizar contenido antes de inyectarlo en prompts del LLM**
  `SearchPlugin` inyecta knowledge items directamente en el contexto del agente. Un item con "Ignore all previous instructions" se propaga al LLM. Mínimo viable: strip de patrones de prompt injection conocidos al momento de ingesta, o loggear cuando el score de similitud es anormalmente alto para un query corto.

- [ ] **Revisar JWT — expiración de 7 días sin refresh rotation**
  Si el refresh token no rota en cada uso, un token comprometido tiene 7 días de vida. Evaluar reducir `ExpiresInDays` o implementar rotation: cada `/auth/refresh` invalida el token anterior y emite uno nuevo.

---

## BLOQUE 1 — Correctitud funcional

> El sistema miente o falla silenciosamente.

- [ ] **Implementar trigger de compactación de `ConversationSummary`**
  `SummaryPlugin.CompactConversation()` existe pero nada lo invoca. Agregar check al inicio de `ExecuteChatUseCase`: si `messages.Count > 20` o `tokensUsed > 8000` (configurable), disparar compactación antes de enviar al agente. Sin esto, conversaciones largas exceden el context window silenciosamente.

- [ ] **Resolver el modelo de identidad dual (`Domain.User` vs `ApplicationUser`)**
  `Domain.User` existe en el dominio pero `ApplicationContext` lo ignora con `builder.Ignore<>()`. Decisión requerida: eliminar `Domain.User` y documentar como ADR-003, o hacer el mapeo explícito. La ambigüedad actual garantiza confusión y bugs de mantenimiento.

- [ ] **Verificar que `GetConversations` y otros GET de listado filtran por `UserId`**
  Confirmar que ningún use case de listado devuelve registros de todos los usuarios por ausencia de filtro. Revisar `GetKnowledgeItems`, `GetConversations`, `GetRepositories`, `GetTrends`.

- [ ] **Conectar `CodeAnalysisBackgroundService` a una implementación real**
  `POST /repositories` acepta la request y devuelve 202, pero el análisis nunca se ejecuta. El usuario queda esperando un resultado que jamás llega. Al menos mostrar estado `Pending` con mensaje claro en el frontend, o hacer fallar el endpoint con un 501 explícito hasta que esté implementado.

- [ ] **Verificar que `TokenUsageTracker` se incrementa en todos los flujos LLM**
  Si el Knowledge Agent o el Guardian Agent invocan el LLM sin pasar por el tracker, la quota no se aplica. Auditar todos los puntos de invocación de modelos y confirmar que `TokenUsageTracker.Increment()` está en cada uno.

- [ ] **Evitar que `NullEmbeddingService` llegue a producción silenciosamente**
  Si `AI:OpenAI:ApiKey` está vacía, el sistema usa `NullEmbeddingService` y todos los embeddings son vectores cero — la búsqueda semántica devuelve basura sin errores visibles. Agregar log de advertencia prominente en startup o un health check que detecte esta condición.

---

## BLOQUE 2 — Deuda arquitectónica

> Código que explota a medida.

- [ ] **Reemplazar keyword fallback en memoria en `SearchPlugin`**
  El `ToListAsync()` + `Where()` en memoria trae todos los items del usuario antes de filtrar. Con pocos miles de items causa OOM o latencia inaceptable. Implementar con PostgreSQL FTS (`tsvector` + `tsquery`) o trigram index (`pg_trgm` + `ILIKE` con índice GIN).

- [ ] **Mover validación de input de endpoints a capa Application**
  `KnowledgeEndpoints.cs` hace validación inline de `Title.Length`, parseo de enum, etc. Eso es lógica de negocio en la capa de presentación. Crear `IngestKnowledgeValidator` en Application y llamarlo desde el use case antes de operar.

- [ ] **Mover records HTTP fuera de las clases de endpoints**
  `IngestKnowledgeHttpRequest`, `IngestKnowledgeHttpResponse` y similares están definidos dentro de `KnowledgeEndpoints`. Moverlos a archivos propios en la carpeta de la feature correspondiente.

- [ ] **Refactor de `ChatEndpoints` según `REFACTORING_PLAN.md`**
  Ya documentado como pendiente de alta prioridad. El endpoint concentra demasiadas responsabilidades. Ejecutar el plan por etapas para reducir riesgo de regresión.

- [ ] **Definir estrategia de tracing de decisiones del agente**
  Actualmente no hay logging de qué plugin fue invocado, con qué argumentos, qué retornó ni cuánto tardó. Mínimo viable: log estructurado con `correlationId`, `pluginName`, `functionName`, `durationMs`, `resultLength`. Imprescindible para debuggear problemas en producción.

- [ ] **Propagar `CancellationToken` en todos los hub methods de SignalR**
  Varios métodos del hub no propagan el token hacia las queries EF Core. En una desconexión abrupta, las operaciones siguen corriendo hasta completarse. Agregar `CancellationToken ct` a cada método y propagarlo.

---

## BLOQUE 3 — Features incompletas

> Con impacto directo en usuarios reales.

- [ ] **Code Guardian — implementar `ICodeAnalyzer` con Roslyn**
  Clone de repo → análisis estático → generación de `AnalysisFinding` → persistencia de `AnalysisReport`. Sin esto el pilar 3 es un botón que no hace nada.

- [ ] **Code Guardian — implementar `DependencyPlugin`**
  Escaneo de NuGet packages contra vulnerabilidades conocidas (OSS Index o NVD). Parte crítica del análisis de repos externos.

- [ ] **Code Guardian — garantizar limpieza de `temp-repos/` tras el análisis**
  El directorio está en `.gitignore` pero el job debe garantizar borrado aunque el análisis falle. Usar `try/finally` en el background service.

- [ ] **Trends Radar — implementar al menos una fuente de scraping**
  `TrendScannerService` existe pero sin fuentes configuradas. Al menos una fuente real (GitHub Trending API, Hacker News Algolia API) para validar el flujo end to end.

- [ ] **Trends Radar — implementar relevance scoring contra `UserProfile`**
  `TechnologyTrend.RelevanceScore` existe pero nada lo calcula. El scoring debe comparar el trend contra `PreferredStack` y `KnownPatterns` del perfil del usuario.

- [ ] **`ArchitectureRecommendation` — conectar al flujo del Consultant Agent**
  La entidad y el endpoint `GET /conversations/{id}/recommendations` existen, pero el Consultant Agent nunca genera ni persiste recomendaciones. Definir cuándo el agente debe emitir una `ArchitectureRecommendation` estructurada vs una respuesta libre.

- [ ] **Frontend — agregar estados de error explícitos en todas las vistas**
  Loading y empty states existen; los error states son inconsistentes. Un 500 del backend no debe dejar la UI congelada. Agregar `error.tsx` o manejo de error state en cada feature que hace fetching.

---

## BLOQUE 4 — Tests

> Deuda que bloquea el crecimiento seguro.

- [ ] **Tests de integración para endpoints de API**
  Usar `Microsoft.AspNetCore.Mvc.Testing` + `Testcontainers`. Cubrir al menos: auth flows, ingestion, search semántica, conversation CRUD, archive, delete.

- [ ] **Tests de ownership y authorization**
  Verificar explícitamente que un usuario no puede acceder a recursos de otro. Estos bugs son silenciosos sin tests dedicados. Cubrir: GET /knowledge de otro usuario, DELETE de item ajeno, JOIN a conversación ajena en SignalR.

- [ ] **Tests para `ConversationHub`**
  Al menos: join/leave, streaming de mensaje, desconexión abrupta, intento de acceso a conversación ajena.

- [ ] **Tests para `SearchPlugin`**
  Verificar que el threshold de similitud filtra correctamente, que el keyword fallback no devuelve items de otros usuarios, que falla gracefully si el embedding service no responde.

- [ ] **Tests de componentes en el frontend**
  `ChatPanel`, `ConversationList`, `RepoPicker` tienen lógica no trivial. Vitest + Testing Library para cubrir los caminos críticos.

- [ ] **Test de regresión para el bug del `streamDoneRef`**
  El typing indicator que quedaba stuck fue un bug sutil de sincronización entre SignalR y React transitions. Documentar el escenario y agregar test que lo cubra antes de que regrese.

---

## BLOQUE 5 — Escalabilidad y operaciones

> Planificar antes de necesitar.

- [ ] **Documentar y preparar Redis backplane para SignalR**
  Ya documentado en `IMPLEMENTATION_LOG.md`. Definir el threshold de activación (N usuarios concurrentes, M instancias). `AddSignalR().AddStackExchangeRedis(connectionString)` es una línea — el trabajo real es la planificación operacional.

- [ ] **Rate limiting por usuario en endpoints LLM**
  El rate limiter actual protege solo `/auth` por IP. Los endpoints que disparan LLM calls (`POST /conversations/{id}/messages`) no tienen límite por usuario. Agregar política con `System.Threading.RateLimiting` (o Redis en multi-nodo) antes de abrir a usuarios externos.

- [ ] **`Idempotency-Key` en `POST /knowledge` y `POST /conversations`**
  Reintentos de red pueden crear duplicados. Implementar: header `Idempotency-Key: <uuid>` + cache en memoria (o Redis) de 24hs que devuelve el response original ante el mismo key.

- [ ] **Definir estrategia de re-embedding ante cambio de modelo**
  Si se cambia `text-embedding-3-small` por otro modelo, todos los vectores existentes son incompatibles. Documentar el proceso: job de migración batch, ventana de mantenimiento, plan de rollback. Sin este plan, un cambio de modelo corrompe la búsqueda silenciosamente.

- [ ] **Agregar `OutputCache` en endpoints de solo lectura**
  `GET /trends`, `GET /repositories/{id}/analysis`, `GET /knowledge` son buenos candidatos. Reducen carga en PostgreSQL sin necesitar Redis. TTL corto (30s–5min según el endpoint).

- [ ] **Definir estrategia de backup de `knowledge_embeddings`**
  Los embeddings son regenerables pero re-generar 50.000 vectores cuesta dinero y tiempo. Un backup de la tabla evita ese costo ante un desastre. Documentar frecuencia, retención y proceso de restore.

---

## BLOQUE 6 — Observabilidad

> Necesario antes de ir a producción real.

- [ ] **Propagar `correlationId` a todos los logs estructurados**
  `CorrelationIdMiddleware` inyecta el ID en el `HttpContext`. Verificar que los `ILogger` en use cases y plugins lo reciben via scope (`ILogger.BeginScope`) y aparece en todos los logs del request.

- [ ] **Agregar métricas de LLM por invocación**
  Por cada llamada al modelo: `durationMs`, `tokensInput`, `tokensOutput`, `modelUsed`, `pluginName`, `success/failure`. Base mínima para controlar costos, detectar degradación de latencia y justificar cambio de modelo.

- [ ] **Health check para providers de AI**
  El health check de readiness solo verifica PostgreSQL. Si la API key de Anthropic expiró o OpenAI tiene un outage, el sistema arranca sin problema y falla en runtime con errores oscuros. Agregar verificación de conectividad a los providers AI en el health check.

- [ ] **Evaluar OpenTelemetry**
  `CorrelationIdMiddleware` es logging básico. Para trazas distribuidas en flows LLM + SignalR + background jobs, OTel con exportador a Jaeger o Datadog es el siguiente nivel natural. Evaluar costo/beneficio cuando la operación crezca.

---

## BLOQUE 7 — Calidad y experiencia de desarrollo

- [ ] **Documentar ADR-003 — decisión final sobre `Domain.User`**
  Sea cual sea la decisión (eliminar o mapear explícitamente), formalizarla en `ARCHITECTURE_DECISIONS.md` y eliminar el código muerto o la confusión.

- [ ] **Validación de configuración crítica en startup**
  Si `Jwt:Key` está vacío, `AI:OpenAI:ApiKey` falta, o `ConnectionStrings:Postgres` no está configurado, el proceso debe fallar en startup con mensaje claro. No en runtime con un 500 oscuro al primer request.

- [ ] **Mover `similarity threshold` a configuración**
  `0.35f` está hardcodeado en `SearchPlugin`. Moverlo a `appsettings.json` bajo `AI:Search:MinimumScore`. Documentar qué significa en términos de precisión vs recall. Opcionalmente: endpoint de admin para ajustarlo en runtime sin redeploy.

- [ ] **Actualizar `IMPLEMENTATION_LOG.md`**
  La feature `Guardian → Consultant RepoBound` figura como en progreso (🔄) pero está completada. El doc se está desfasando del código — actualizar el estado antes de que sea inútil.

- [ ] **Agregar `CONTRIBUTING.md` con instrucciones de setup local**
  Un dev nuevo no puede levantar el proyecto sin buscar en el código cómo configurarlo. Documentar: requisitos (PostgreSQL + pgvector, .NET 9), setup de `user-secrets`, claves de API necesarias, comando para aplicar migrations, cómo levantar el frontend.

---

## Orden de ejecución recomendado

```
Bloque 0 (completo)
  → Bloque 1 (completo)
    → Bloque 2 ítems críticos (keyword search, validación, ChatEndpoints)
      → Bloque 3 por pilar (Guardian primero, luego Radar, luego Recommendations)
        → Bloque 4 + Bloque 6 en paralelo
          → Bloque 5 cuando el volumen lo justifique
            → Bloque 7 continuo a lo largo de todo
```

**Total: 40 ítems** en 8 bloques.
