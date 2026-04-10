# Deuda Técnica

Hallazgos del code review del 2026-04-10. Ninguno de estos items rompe la aplicación ni impide su funcionamiento actual. Son mejoras pendientes para incrementos futuros.

---

## 1. Multi-tenancy incompleta en entidades del dominio

**Severidad:** Media  
**Capa:** Domain  
**Rompe la app:** No

Las siguientes entidades no tienen `UserId` y/o `TenantId`, lo cual va en contra de la convención del proyecto (toda entidad debe soportar multi-tenancy).

| Entidad | Falta |
|---|---|
| `TechnologyTrend` | `UserId` + `TenantId` |
| `Tag` | `UserId` + `TenantId` |
| `AnalysisReport` | `UserId` + `TenantId` |
| `AnalysisFinding` | `UserId` + `TenantId` |
| `ContentPublishRequest` | `TenantId` |
| `UserProfile` | `TenantId` |
| `ProfileUpdateSuggestion` | `TenantId` |

**Consideración de diseño:** `TechnologyTrend` y `Tag` pueden ser entidades globales (compartidas por todos los tenants), en cuyo caso esta deuda no aplicaría para ellas. Definir esto antes de agregar los campos.

**Impacto futuro:** Si se implementan filtros de scope por tenant en estos recursos, no va a ser posible sin agregar los campos primero (requiere migración de DB).

---

## 2. Inicialización de colecciones con `[]` en lugar de tipo explícito

**Severidad:** Baja (cosmético)  
**Capa:** Domain  
**Rompe la app:** No

Tres entidades usan `[]` en lugar del tipo explícito requerido por la convención del proyecto (`new HashSet<T>()` o `new List<string>()`).

| Entidad | Propiedad | Convención esperada |
|---|---|---|
| `TechnologyTrend` | `Sources` | `new List<string>()` |
| `Tag` | `KnowledgeItemTags` | `new HashSet<KnowledgeItemTag>()` |
| `UserProfile` | `PreferredStack`, `KnownPatterns` | `new List<string>()` |

**Nota:** `[]` en C# 12+ es funcionalmente equivalente. El compilador infiere el tipo correcto. Es pura consistencia de convención.

---

## 3. Índice único faltante en `ApplicationUser.Email`

**Severidad:** Media  
**Capa:** Data (`IdentityConfigurations.cs`)  
**Rompe la app:** No

El `ApplicationUserConfiguration` no configura un índice único sobre la propiedad `Email`. La migración inicial crea un índice sobre `NormalizedEmail` pero sin la constraint `UNIQUE`.

**Riesgo real:** Sin este índice, en teoría podrían existir dos usuarios con el mismo email si se producen condiciones de carrera o inserciones directas por SQL. ASP.NET Identity tiene validación a nivel de aplicación, pero no hay garantía a nivel de base de datos.

**Fix:**
```csharp
// En ApplicationUserConfiguration.Configure()
builder.HasIndex(u => u.Email).IsUnique();
```

Requiere una migración nueva.

---

## 4. PKs Guid sin `HasDefaultValueSql` explícito en configuraciones EF Core

**Severidad:** Baja  
**Capa:** Data (todas las configuraciones de entidades)  
**Rompe la app:** No

Ninguna configuración de entidad define explícitamente cómo se generan los Guid PKs. La convención del proyecto indica que debería usarse `HasDefaultValueSql("gen_random_uuid()")` o documentar que la generación es del lado del cliente.

Actualmente EF Core genera los Guids client-side antes del INSERT, por lo que funciona correctamente. Solo sería un problema si alguien insertara filas directamente por SQL sin pasar por EF Core.

**Fix (opcional):** Agregar en cada configuración de entidad:
```csharp
builder.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
```

O alternativamente, agregar un comentario en `BaseEntity` documentando que la generación es client-side intencionalmente.

---

## Prioridad sugerida

| Prioridad | Item |
|---|---|
| 1 | Índice único en Email (riesgo de integridad de datos) |
| 2 | Multi-tenancy en entidades (antes de lanzar multi-tenant real) |
| 3 | Guid PKs explícitos (opcional, puramente cosmético) |
| 4 | Colecciones con `[]` (cosmético) |
