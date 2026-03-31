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
