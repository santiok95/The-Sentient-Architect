# The Sentient Architect — Implementation Log

## Status Legend
- ✅ Completed
- 🔄 In Progress
- 📋 Planned
- 🐛 Issue Found

## Phase 0 — Analysis & Design (Current)

| Item | Status | Notes |
|------|--------|-------|
| Define 4 pillars | ✅ | Semantic Brain, Architecture Consultant, Code Guardian, Trends Radar |
| Competitive landscape | ✅ | No unified tool exists; integration is our differentiator |
| Architecture pattern | ✅ | Clean Architecture, 6 projects (Domain, Application, Data, Data.Postgres, Infrastructure, API) |
| Entity model | ✅ | 15 entities across 4 pillars + shared |
| Database decision | ✅ | PostgreSQL + pgvector; IVectorStore interface for future migration |
| Runtime decision | ✅ | .NET 9 stable |
| ConversationSummary design | ✅ | Rolling summary for token cost management |
| UserProfile update mechanism | ✅ | Suggestion-based with user confirmation |
| Repo trust levels | ✅ | Internal (quality focus) vs External (full security scan) |
| Semantic Kernel agent design | ✅ | 3 agents (Knowledge, Consultant, Guardian) + 1 background job (Radar) |
| Semantic Kernel rule file | ✅ | .claude/rules/semantic-kernel.md created |
| API contracts | ✅ | 7 endpoint groups + 3 SignalR hubs. Minimal APIs with /api/v1/ versioning |
| Security & auth design | ✅ | ASP.NET Identity + JWT, Admin/User roles, content scope (personal + shared with approval) |
| Stack with versions | ✅ | Full NuGet stack documented in PROJECT_CONTEXT.md |

## Phase 1 — Semantic Brain
| Item | Status | Notes |
|------|--------|-------|
| Solution scaffold | ✅ | 6 projects: Domain, Application, Data, Data.Postgres, Infrastructure, API. .slnx format |
| Domain entities | ✅ | KnowledgeItem, KnowledgeEmbedding, Tag, KnowledgeItemTag, User, UserProfile, ContentPublishRequest, ProfileUpdateSuggestion, TokenUsageTracker. BaseEntity inheritance, private setters, behavior methods |
| Domain enums | ✅ | KnowledgeItemType, ProcessingStatus, TagCategory, PublishRequestStatus, SuggestionStatus, QuotaAction |
| Domain value objects | ✅ | VectorSearchResult record |
| Domain interfaces | ✅ | IKnowledgeRepository, IVectorStore, IEmbeddingService, ITagRepository (all in Application layer) |
| Identity (User entity) | ✅ | User POCO in Domain + ApplicationUser : IdentityUser\<Guid\> in Data. Table renamed to "Users" |
| EF Core DbContext | ✅ | ApplicationContext in Data, IdentityDbContext\<ApplicationUser\>, pgvector extension, ApplyConfigurationsFromAssembly |
| Identity table renaming | ✅ | All 7 AspNet* tables renamed (Users, Roles, UserRoles, RoleClaims, UserClaims, UserLogins, UserTokens) |
| Fluent API configs | ✅ | All entities configured. Enums as `.HasConversion<string>().HasMaxLength(50)`. JSONB for List\<string\>. FK via ADR-002 |
| pgvector setup | ✅ | vector(1536) hardcoded (ADR-001), HNSW index with cosine ops |
| ADR-001 | ✅ | Hardcoded vector(1536) for HNSW performance |
| ADR-002 | ✅ | FK via Fluent API without navigation property (cross-layer) |
| Ingestion pipeline | ✅ | IngestKnowledgeUseCase: chunk → embed (NullEmbeddingService) → pgvector |
| RAG query flow | ✅ | SearchKnowledgeUseCase: embed query → IVectorStore → fetch items |
| Auth endpoints | ✅ | POST /auth/register + /auth/login. JWT, Identity, IdentitySeeder |
| Knowledge endpoints | ✅ | POST/GET/DELETE /knowledge, GET /knowledge/search |
| Repository implementations | ✅ | KnowledgeRepository, TagRepository, PgVectorStore (raw SQL HNSW) |
| NullEmbeddingService | ✅ | Placeholder in Infrastructure/AI/ — still used as graceful fallback when OpenAI key absent |
| OpenAIEmbeddingService | ✅ | Real implementation in Infrastructure/AI/ — active when AI:OpenAI:ApiKey is configured |

## Phase 2 — Architecture Consultant
| Item | Status | Notes |
|------|--------|-------|
| Conversation entities | ✅ | Conversation + ConversationMessage. Behavior: AddMessage, UpdateSummary, Archive |
| EF configs + migration | ✅ | Migration: AddPhase2345Tables (2026-04-01) |
| UserProfile + suggestions | ✅ | UserProfile Update* methods, ProfileUpdateSuggestion Accept/Reject |
| Conversation use cases | ✅ | CreateConversation, GetConversations, ArchiveConversation |
| Profile use cases | ✅ | GetProfile, UpdateProfile, AcceptSuggestion, RejectSuggestion |
| Admin use cases | ✅ | ReviewPublishRequest (Approve/Reject) |
| Conversation/Profile endpoints | ✅ | Full CRUD. Admin policy: RequireAuthorization("Admin") |
| ConversationSummary compaction | 📋 | SummaryPlugin.SaveConversationSummaryAsync implemented — needs token threshold trigger |
| Semantic Kernel agent — Knowledge | ✅ | ChatCompletionAgent with SearchPlugin + IngestPlugin. Anthropic Claude via IChatClient bridge |
| Semantic Kernel agent — Consultant | ✅ | ChatCompletionAgent with ProfilePlugin + SummaryPlugin + SearchPlugin |
| OpenAI embeddings | ✅ | OpenAIEmbeddingService wrapping ITextEmbeddingGenerationService (text-embedding-3-small, 1536 dims) |
| Anthropic chat integration | ✅ | AnthropicClient → AsBuilder() → UseFunctionInvocation() → AsChatCompletionService() bridge |
| SignalR streaming | ✅ | ConversationHub, AnalysisHub e IngestionHub implementados (falta asegurar membresía a grupos) |

## Phase 3 — Code Guardian
| Item | Status | Notes |
|------|--------|-------|
| RepositoryInfo entities | ✅ | RepositoryInfo, AnalysisReport, AnalysisFinding + enums |
| EF configs | ✅ | Included in AddPhase2345Tables migration |
| Use cases | ✅ | SubmitRepository, GetRepositories, GetAnalysisReport |
| Repository endpoints | ✅ | POST/GET /repositories, GET /repositories/{id}/reports, GET /reports/{id} |
| Git clone service | 📋 | ICodeAnalyzer not implemented yet |
| Roslyn analysis | 📋 | |
| Dependency scanner | 📋 | |
| AnalysisReport generation | 📋 | Background job — needs Roslyn + Git integration |

## Phase 4 — Trends Radar
| Item | Status | Notes |
|------|--------|-------|
| TechnologyTrend entities | ✅ | TechnologyTrend + TrendSnapshot + enums |
| EF configs | ✅ | Included in AddPhase2345Tables migration |
| Use cases | ✅ | GetTrends (with optional category filter), GetTrendSnapshots |
| Trend endpoints | ✅ | GET /trends, GET /trends/{id}/snapshots (public, no auth) |
| Source configuration | 📋 | Where to scan for trends (GitHub, HN, etc.) |
| Scanning background job | 📋 | IHostedService — needs AI + sources |
| Relevance scoring | 📋 | |

## Refactoring — Modernization (2026-03-31)

| # | Task | Status | Priority | Notes |
|---|------|--------|----------|-------|
| 1 | Fix connection string key | ✅ | Alta | Changed "DefaultConnection" → "Postgres" in appsettings |
| 2 | Enum string conversions | ✅ | Alta | `.HasConversion<string>().HasMaxLength(50)` in all 6 configs |
| 3 | Alinear docs | ✅ | Alta | CLAUDE.md, clean-architecture.md, entity-framework.md |
| 4 | BaseEntity abstracta | ✅ | Media | Id + CreatedAt centralized |
| 5 | Private setters + behavior methods | ✅ | Media | All entities modernized |
| 6 | Value Objects (TechStack, PatternList) | ⬜ | Media | Deferred to Phase 2 (Consultant Agent) |
| 7 | Métodos de comportamiento | ✅ | Media | MarkAsCompleted, Approve, Reject, etc. |
| 8 | IVectorStore scope fix | ✅ | Media | Added userId, tenantId, includeShared params |
| 9 | Evaluar ErrorOr vs Result | ⬜ | Baja | Pending decision |
| 10 | Vertical slices | ⬜ | Baja | Se aplica cuando arranque implementación de features |
| 11 | Centralizar Identity en Infra | ✅ | Alta | Desacoplar `AddIdentityCore` de PostgreSQL para pureza arquitectónica |
| 12 | Desacople de Chat (Etapa 1) | ✅ | Alta | Orquestación movida de API a Application e Infrastructure |

## Architecture Decisions — Chat Decoupling (2026-04-03)

### Stage 1 — Orchestration & Decoupling
- **What:** Se movió la orquestación del flujo de chat (loops de herramientas, prompting y lógica de agentes) desde los endpoints de la API hacia `ExecuteChatUseCase` (Application) e `IChatExecutionService` (Infrastructure).
- **Why:** Reducir la complejidad de los endpoints (Fat Controller), mejorar la mantenibilidad y desacoplar la lógica de negocio de los detalles técnicos del proveedor de IA (Semantic Kernel/Anthropic).
- **Lección Aprendida:** El endpoint debe limitarse a coordinar el transporte (SignalR/HTTP) y el mapeo de resultados; la "mecánica" de la IA debe estar aislada.

### Pattern — Isolated Anthropic Manual Loop
- **What:** Se encapsuló el loop manual de herramientas (while + parseo de bloques invoke/parameter) en un `AnthropicOrchestrator` dentro de Infrastructure.
- **Why:** El bridge nativo entre SK y Claude 3.5 Sonnet ha demostrado inestabilidades en el parseo de herramientas. Aislar este "workaround" protege la estabilidad de las capas superiores.
- **Lección Aprendida:** Si un bridge de terceros falla, el fallback manual debe implementarse como un adaptador de infraestructura, nunca filtrarse al caso de uso ni al endpoint.

### Standard — Result Pattern End-to-End
- **What:** Se estandarizó el uso de `Result<T>` en todo el flujo: validación de entrada -> persistencia de mensaje de usuario -> ejecución de chat -> persistencia de respuesta del asistente -> mapeo final a HTTP.
- **Why:** Garantizar un flujo predecible, evitar excepciones de control y asegurar que los errores en el flujo de SignalR (`ReceiveError`) se reporten de forma coherente.
- **Lección Aprendida:** Un flujo de resultados consistente simplifica drásticamente el manejo de errores en sistemas asíncronos y distribuidos como SignalR.

## Issues & Solutions
_None yet — will be populated during implementation._

## Follow-ups Agendados (No Bloqueantes)

| Item | Status | Notes |
|------|--------|-------|
| CORS endurecimiento para despliegue | 📋 | Deferred por decision explicita del owner. Mantener policy actual en dev; antes de deploy cambiar a origins explicitos por ambiente. |
| Autorizacion de membresia a grupos SignalR | 📋 | Pendiente: validar ownership/tenant antes de `AddToGroupAsync` en ConversationHub, AnalysisHub e IngestionHub. |
| Refactor ChatEndpoints (separacion de responsabilidades) | 📋 | Prioridad alta. Implementar plan por etapas definido en `docs/REFACTORING_PLAN.md` para reducir riesgo de regresion. |
| SearchPlugin keyword fallback escalable | 📋 | Pendiente: mover filtro de keywords a DB (FTS/trigram/ILIKE indexado), evitar `ToListAsync` + filtrado en memoria en datasets grandes. |

---

## Future Scalability (Phase X) — Redis Topology

Como plan de arquitectura comprobada a futuro, cuando la plataforma alcance la necesidad de escalar horizontalmente (múltiples nodos de backend) o maneje picos severos de usuarios concurrentes, se agregará un tier de **Redis (In-Memory Data Store)**. 

Redis **no reemplazará a PostgreSQL**, funcionará como una capa flotante enfocada a tres puntos críticos:

1. **SignalR Backplane**: Para permitir escalamiento horizontal. Cuando existen múltiples réplicas de la Minimal API, Redis orquestará los WebSockets. Si un background worker finaliza un token de la IA en la *Réplica A* y el usuario está conectado en la *Réplica C*, el Backplane rutea por pub/sub el evento y mantiene el stream sincronizado sin pérdida de datos.
2. **Distributed Caching (`IDistributedCache`)**: Para aliviar queries pesadas de PostgreSQL. Data predominantemente estática como los **Trends Snapshots** de la Fase 4 o los análisis históricos pesados de un repositorio, se cachearán con invalidación en Redis. Time-To-Live (TTL) estricto.
3. **Rate Limiting Estricto y Centralizado**: Como escudo financiero. Limitar la cantidad extrema de requests hacia APIs de IA pagas (Ej: *15 msg/min por Token JWT*). Redis resolverá estos "counters" eficientemente y compartirá el conteo a través de los nodos independientemente del Load Balancer que esté operando delante de la API.
4. **Distributed Locking (Redlock)**: Crítico para esquivar 'Race Conditions' en operaciones destructivas o costosas. Ejemplo en Code Guardian: si un usuario aprieta 3 veces el botón de "Analizar repo", usamos un lock en Redis con la URL de GitHub. Si un worker lo está clonando, los otros abortan y evitan sobrescribir la carpeta `temp-repos/`.
5. **Semantic Caching (Redis Stack)**: Evaluar cachear vectores además de strings usando Redis Stack. Si el query del chat *"¿Cómo escalo SignalR?"* es semánticamente idéntico a *"Mejor forma de escalar WebSockets en .NET"*, Redis intersecta los embeddings y devuelve el pre-computado sin gastar tokens en el LLM. El "escudo financiero" definitivo.
6. **Task Message Broker (Resiliencia de Background Jobs)**: Actuar como cola rápida para Hangfire o MassTransit durante la ingesta masiva (Fase 1). Si un nodo se apaga por OOM mientras genera embeddings, la tarea no se purga, sino que vuelve a la cola y un nodo sano la asume.

### Consideraciones Técnicas de Implementación (The Architect's Checklist)

- **ConnectionMultiplexer forzado a Singleton**: En .NET la creación de conexiones Redis es cara (`ConnectionMultiplexer`). JAMÁS registrarlo como Transient o Scoped, siempre como `AddSingleton()` para evitar _socket exhaustion_.
- **Estrategia C-UD (Create-Update/Delete) para Invalidación:** Todo `OutputCache` o caching de reportes (Guardian) debe ser invalidado forzosamente si un usuario ejecuta un *Re-Analyze* o un *AcceptSuggestion*, evitando latencia de estado visual obsoleto.
- **TLS Obligatorio en Redis:** Incluso si está en una VPC (Virtual Private Cloud), usar encripción en tránsito hacia la instancia de Redis si en algún momento se deciden cachear los PII del UserProfile.