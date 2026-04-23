# Security Backlog

Cosas a hacer cuando la app escale o cuando haya tiempo. Ordenado por prioridad real.

---

## 🔴 Bloqueante para escalado horizontal

### Migrar rate limit state de `IMemoryCache` a Redis

**Problema**: `ILoginAttemptTracker` y `IUserChatThrottleService` viven en `IMemoryCache` (proceso único). Con 2+ réplicas del backend:
- Cada pod tiene su propio contador → atacante tiene `N × limit` intentos
- Restart del contenedor resetea todos los counters

**Afecta**:
- `apps/backend/SentientArchitect.API/Filters/InMemoryLoginAttemptTracker.cs`
- `apps/backend/SentientArchitect.API/Filters/InMemoryUserChatThrottleService.cs`
- El rate limiter built-in de `AddRateLimiter` (`Program.cs:104-154`) también es in-memory

**Fix**:
- Agregar `StackExchange.Redis` + `Microsoft.Extensions.Caching.StackExchangeRedis`
- Nuevas implementaciones `RedisLoginAttemptTracker` y `RedisUserChatThrottleService`
- Para el rate limiter global: usar partitioned limiter con estado en Redis (o reemplazarlo por middleware custom que cuente en Redis)
- Registrar con feature flag: si `Redis:Enabled=true`, usar Redis; sino, in-memory (para dev)

**Cuándo**: ANTES de levantar 2+ réplicas en producción. Hoy con 1 instancia está ok.

---

## 🟡 Importante pero no urgente

### Revocación real de sesiones

**Problema actual**:
- `LogoutSessionUseCase` NO hace nada — solo valida `UserId != Empty` y devuelve `Success`. Un logout en el frontend limpia localStorage, pero si alguien tiene el JWT capturado, sigue siendo válido hasta expirar.
- `RefreshSessionUseCase` usa el MISMO access token con `allowExpired: true` como "refresh". No hay refresh token real separado.

**Mitigado parcialmente**: Bajamos el TTL del JWT a 1 hora (2026-04-22). Ventana de exposición pequeña.

**Fix correcto** (~1-2 días):
1. Entidad `RefreshToken` (Id, UserId, TokenHash, ExpiresAt, RevokedAt, ReplacedByTokenId, CreatedByIp)
2. Login genera access (15-30min) + refresh (7 días) — guarda el refresh en DB
3. `/refresh` valida el refresh contra DB, revoca el viejo, emite nuevo par (rotación estricta)
4. `/logout` marca el refresh token activo como `RevokedAt = UtcNow`
5. Opcional: endpoint `/sessions` para que el usuario vea/revoque sus sesiones

### Email verification obligatoria para nuevos registros

**Contexto**: Registro público deshabilitado temporalmente (2026-04-22) en `AuthEndpoints.cs` — devuelve 403. Los registros hoy pasan solo por el seeder/admin.

**Para reabrir registro**:
1. `User.EmailConfirmed` flag (ya existe en ASP.NET Identity)
2. Enviar email con token firmado al registrarse
3. Endpoint `/auth/verify-email?token=...`
4. Login rechaza si `EmailConfirmed == false`

### CAPTCHA en login y (futuro) register

**Por qué**: Proxies rotativos evaden rate limit por IP. Un bot suficientemente motivado te crea cuentas / intenta passwords todo el día.

**Fix**: Cloudflare Turnstile (free tier, sin cookies, sin puzzles molestos).
- Frontend: `<Turnstile siteKey={...} />` en el form, token en el body
- Backend: endpoint filter que valida el token contra Cloudflare antes de dejar pasar al UseCase

### Token budget diario — mejoras pendientes

**Hecho (2026-04-22)**:
- Gate en `ChatMessageLimitsFilter` ANTES de llamar al LLM (rechaza 429 si el día superó quota)
- Tracking post-ejecución ya existía en `ChatExecutionService.TrackTokenUsageAsync` (estimación 1 token ≈ 4 chars)
- Default: 100K tokens/día por usuario, Admin bypassa (configurable)

**Faltante**:
1. Usar tokens REALES del response del LLM (Semantic Kernel expone `Usage` en el result) en lugar de estimación por chars
2. Estimar prompt tokens CON `SharpToken`/`Tiktoken` antes de llamar al LLM y sumarlos al chequeo
3. Exponer `/api/v1/me/usage` para que el usuario vea su consumo del día
4. Notificación al usuario cuando llega a 80% (`IsNearQuota` ya existe en la entidad)
5. `QuotaAction.DegradeModel` — si está near quota, cambiar a un modelo más barato en lugar de bloquear

---

## 🟢 Hardening — backlog largo

### JWT en httpOnly cookie (fuera de localStorage)

**Problema**: JWT en `localStorage` es vulnerable a XSS. Mitigado por `SameSite=Strict` en la cookie `sa_token`, pero si hay XSS, el token se va.

**Fix** (~1-2 días):
1. Login → backend setea cookie `Set-Cookie: sa_token=...; HttpOnly; Secure; SameSite=Strict; Path=/`
2. `apiClient` en frontend usa `credentials: 'include'` en todos los fetch — NO lee el token
3. Eliminar `getToken()` de `apps/frontend/src/lib/auth.ts`
4. SignalR: `accessTokenFactory` no sirve con httpOnly — usar query param firmado de corta duración (`/api/v1/signalr/ticket` emite ticket de 60s) o configurar SignalR con `withCredentials: true` y que el backend valide la cookie en el handshake
5. CSRF: con `SameSite=Strict` ya estás cubierto para el caso típico, pero agregar double-submit token si querés defense-in-depth

### HSTS + Cloudflare SSL verificar panel

**No es código, es configuración del panel**:
- Cloudflare → SSL/TLS → Mode = **Full (strict)** (NO Flexible)
- Cloudflare → SSL/TLS → Edge Certificates → **Always Use HTTPS = ON**
- Cloudflare → SSL/TLS → Edge Certificates → **HSTS = ON**, max-age 6 meses, include subdomains
- Coolify → proxy debería forzar HTTPS end-to-end

Con eso y el `SameSite=Strict` de la cookie, estás cubierto sin tocar el backend.

### Structured audit log de eventos de auth

**Qué loguear** (con `ILogger` + sink a ELK/Loki/CloudWatch):
- Login exitoso / fallido (email hasheado)
- Rate limit rejected (ya loguea, bien)
- Password change
- Logout
- Token refresh
- Admin actions (publicar contenido, cambiar roles)

Con esto podés alertar sobre patterns sospechosos (muchos logins fallidos desde IPs de un mismo ASN, etc.)

---

## Completado

- ✅ 2026-04-22 — JWT TTL bajado a 1 hora (era 7 días). `appsettings.json`, `docker-compose.yml`, `TokenService.cs`.
- ✅ 2026-04-22 — Endpoint `/api/v1/auth/register` deshabilitado (responde 403). `AuthEndpoints.cs`.
- ✅ 2026-04-22 — Página `register/page.tsx` reducida a `redirect('/login')` + JSX huérfano removido.
- ✅ 2026-04-22 — CORS: fail-fast en producción si `Cors:AllowedOrigins` está vacío + log de orígenes permitidos al startup.
- ✅ 2026-04-22 — Verificado: los 3 SignalR hubs (`ConversationHub`, `IngestionHub`, `AnalysisHub`) tienen `[Authorize]` + validación de ownership del recurso antes de `AddToGroupAsync`. OK, no se tocó.
- ✅ 2026-04-22 — Max length del mensaje (12.000 chars por default, configurable) + gate de presupuesto diario (100K tokens/día, Admin bypassa) en `ChatMessageLimitsFilter`. Chat filter chain: `ChatMessageLimitsFilter` → `ChatThrottleFilter`.
