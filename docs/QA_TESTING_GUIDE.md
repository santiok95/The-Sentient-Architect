# QA Testing Guide — The Sentient Architect API

Instructivo paso a paso para verificar que toda la API funciona correctamente.

---

## Prerequisitos

### 1. PostgreSQL con pgvector

Levantar un contenedor de PostgreSQL con pgvector:

```bash
docker run -d \
  --name sentient-postgres \
  -e POSTGRES_USER=postgres \
  -e POSTGRES_PASSWORD=tu_password_seguro \
  -e POSTGRES_DB=SentientArchitectDb \
  -p 5433:5432 \
  pgvector/pgvector:pg16
```

### 2. Configuración

Verificar que `src/SentientArchitect.API/appsettings.Development.json` tenga:

| Clave | Requerida | Nota |
|-------|-----------|------|
| `ConnectionStrings:Postgres` | Sí | Conexión a PostgreSQL (puerto 5433 por defecto en dev) |
| `Jwt:Key` | Sí | Mínimo 32 caracteres |
| `Seeder:AdminEmail` | No | Si está vacío, no se crea admin al arrancar |
| `Seeder:AdminPassword` | No | Si está vacío, no se crea admin al arrancar |
| `AI:Anthropic:ApiKey` | No* | Sin esta clave, el chat y TrendScanner degradan silenciosamente |
| `AI:OpenAI:ApiKey` | No* | Sin esta clave, se usa NullEmbeddingService (búsqueda vectorial no funciona) |

> *La app arranca sin claves de IA, pero las funcionalidades de chat, embeddings y análisis de tendencias NO van a funcionar.

### 3. Aplicar migraciones y arrancar

```bash
cd src
dotnet ef database update --project SentientArchitect.Data.Postgres --startup-project SentientArchitect.API
dotnet run --project SentientArchitect.API
```

La app corre en:
- HTTP: `http://localhost:5291`
- HTTPS: `https://localhost:7242`

### 4. Scalar (API Docs)

En entorno Development, la documentación interactiva está disponible en:
- UI: `http://localhost:5291/scalar/v1`
- OpenAPI spec: `http://localhost:5291/openapi/v1.json`

---

## Variables para las pruebas

A lo largo del instructivo, guardar estos valores que se van obteniendo:

```
TOKEN_USER    = (JWT del usuario normal)
TOKEN_ADMIN   = (JWT del admin)
USER_ID       = (GUID del usuario)
CONVERSATION_ID = (GUID de la conversación)
KNOWLEDGE_ID  = (GUID del knowledge item)
REPO_ID       = (GUID del repositorio)
```

> **Herramienta recomendada:** Postman, Insomnia, o `curl`. Para SignalR usar el cliente de prueba de Postman o un script JS.

---

## Fase 1 — Autenticación

### Test 1.1: Registrar usuario normal

```http
POST /api/v1/auth/register
Content-Type: application/json

{
  "email": "qa-tester@test.com",
  "password": "TestPassword1",
  "displayName": "QA Tester"
}
```

**Esperado:** `201 Created`
```json
{
  "userId": "<GUID>",
  "email": "qa-tester@test.com",
  "displayName": "QA Tester"
}
```
> Guardar `userId` como `USER_ID`.

### Test 1.2: Registrar con email duplicado

Repetir el mismo request anterior.

**Esperado:** `400 Bad Request` con errores de Identity.

### Test 1.3: Registrar con password débil

```json
{
  "email": "weak@test.com",
  "password": "123",
  "displayName": "Weak"
}
```

**Esperado:** `400 Bad Request` — password requiere mínimo 8 caracteres, 1 dígito, 1 minúscula, 1 mayúscula.

### Test 1.4: Login con usuario normal

```http
POST /api/v1/auth/login
Content-Type: application/json

{
  "email": "qa-tester@test.com",
  "password": "TestPassword1"
}
```

**Esperado:** `200 OK`
```json
{
  "token": "<JWT>",
  "userId": "<GUID>",
  "email": "qa-tester@test.com",
  "displayName": "QA Tester",
  "expiresAt": "<fecha +7 días>"
}
```
> Guardar `token` como `TOKEN_USER`.

### Test 1.5: Login con credenciales incorrectas

```json
{
  "email": "qa-tester@test.com",
  "password": "WrongPassword1"
}
```

**Esperado:** `401 Unauthorized` (sin body).

### Test 1.6: Login con admin (si se configuró el seeder)

```http
POST /api/v1/auth/login
Content-Type: application/json

{
  "email": "admin@sentientarchitect.dev",
  "password": "Admin123!"
}
```

**Esperado:** `200 OK` con JWT.
> Guardar `token` como `TOKEN_ADMIN`.

---

## Fase 2 — Perfil de Usuario

> Todos los requests de aquí en adelante usan header: `Authorization: Bearer <TOKEN_USER>`

### Test 2.1: Obtener perfil (inicial vacío)

```http
GET /api/v1/profile
Authorization: Bearer <TOKEN_USER>
```

**Esperado:** `200 OK` — perfil con listas vacías o valores por defecto.

### Test 2.2: Actualizar perfil

```http
PUT /api/v1/profile
Authorization: Bearer <TOKEN_USER>
Content-Type: application/json

{
  "preferredStack": ["C#", ".NET 9", "PostgreSQL", "Angular"],
  "knownPatterns": ["Clean Architecture", "CQRS", "Result Pattern"],
  "infrastructureContext": "Docker + Azure Kubernetes",
  "teamSize": "5-10",
  "experienceLevel": "Senior",
  "customNotes": "Enfocado en microservicios y event-driven architecture"
}
```

**Esperado:** `200 OK` — devuelve el perfil actualizado con los valores enviados.

### Test 2.3: Verificar que el perfil persistió

Repetir `GET /api/v1/profile`.

**Esperado:** Los datos del PUT anterior están presentes.

### Test 2.4: Obtener sugerencias de perfil

```http
GET /api/v1/profile/suggestions
Authorization: Bearer <TOKEN_USER>
```

**Esperado:** `200 OK` — lista vacía (no hay sugerencias aún).

### Test 2.5: Request sin autenticación

```http
GET /api/v1/profile
(sin header Authorization)
```

**Esperado:** `401 Unauthorized`.

---

## Fase 3 — Conversaciones

### Test 3.1: Crear conversación

```http
POST /api/v1/conversations
Authorization: Bearer <TOKEN_USER>
Content-Type: application/json

{
  "title": "Mi primera consulta de arquitectura"
}
```

**Esperado:** `201 Created`
```json
{
  "conversationId": "<GUID>",
  "title": "Mi primera consulta de arquitectura"
}
```
> Guardar `conversationId` como `CONVERSATION_ID`.

### Test 3.2: Crear conversación sin título

```http
POST /api/v1/conversations
Authorization: Bearer <TOKEN_USER>
Content-Type: application/json

{}
```

**Esperado:** `201 Created` con título `"New Conversation"`.

### Test 3.3: Listar conversaciones

```http
GET /api/v1/conversations
Authorization: Bearer <TOKEN_USER>
```

**Esperado:** `200 OK` — lista con las 2 conversaciones creadas, cada una con `status: "Active"`.

### Test 3.4: Eliminar conversación

Usar el ID de la conversación sin título:

```http
DELETE /api/v1/conversations/<ID_SIN_TITULO>
Authorization: Bearer <TOKEN_USER>
```

**Esperado:** `204 No Content`.

### Test 3.5: Eliminar conversación inexistente

```http
DELETE /api/v1/conversations/00000000-0000-0000-0000-000000000000
Authorization: Bearer <TOKEN_USER>
```

**Esperado:** `404 Not Found` con ProblemDetails.

---

## Fase 4 — Chat con Agente (requiere API key de Anthropic)

> Si `AI:Anthropic:ApiKey` no está configurada, estos tests van a fallar o devolver error.

### Test 4.1: Conectar a SignalR

Conectar al hub de conversaciones:

```
URL: wss://localhost:7242/hubs/conversation?access_token=<TOKEN_USER>
```

Una vez conectado, invocar:
```json
{ "type": 1, "target": "JoinConversation", "arguments": ["<CONVERSATION_ID>"] }
```

### Test 4.2: Enviar mensaje al Knowledge Agent

```http
POST /api/v1/conversations/<CONVERSATION_ID>/chat
Authorization: Bearer <TOKEN_USER>
Content-Type: application/json

{
  "message": "¿Qué es Clean Architecture?",
  "agentType": "Knowledge"
}
```

**Esperado:**
- HTTP: `200 OK` (el body puede estar vacío — el contenido llega por SignalR)
- SignalR: recibir eventos `ReceiveToken` con chunks del texto y finalmente `ReceiveComplete`

### Test 4.3: Enviar mensaje al Consultant Agent

```http
POST /api/v1/conversations/<CONVERSATION_ID>/chat
Authorization: Bearer <TOKEN_USER>
Content-Type: application/json

{
  "message": "Necesito diseñar un sistema de notificaciones para 100k usuarios",
  "agentType": "Consultant"
}
```

**Esperado:** Similar al anterior — tokens por SignalR con respuesta del agente consultor.

### Test 4.4: Chat en conversación inexistente

```http
POST /api/v1/conversations/00000000-0000-0000-0000-000000000000/chat
Authorization: Bearer <TOKEN_USER>
Content-Type: application/json

{
  "message": "Hola",
  "agentType": "Knowledge"
}
```

**Esperado:** `404 Not Found`.

---

## Fase 5 — Knowledge Base

### Test 5.1: Ingestar artículo

```http
POST /api/v1/knowledge
Authorization: Bearer <TOKEN_USER>
Content-Type: application/json

{
  "title": "Guía de Result Pattern en C#",
  "originalContent": "El Result Pattern es una alternativa al uso de excepciones para manejar errores en la capa de aplicación. En lugar de lanzar excepciones, los métodos retornan un objeto Result que contiene el valor de éxito o una lista de errores. Esto hace el flujo de errores explícito y predecible, mejora la performance al evitar el costo de las excepciones, y facilita el testing. En .NET moderno, se implementa con un record o clase que tiene propiedades IsSuccess, Data y Errors.",
  "type": 0,
  "sourceUrl": "https://example.com/result-pattern",
  "tags": ["csharp", "patterns", "error-handling"]
}
```

**Esperado:** `201 Created`
```json
{
  "knowledgeItemId": "<GUID>",
  "status": "Pending|Processing|Completed",
  "chunksCreated": <número>
}
```
> Guardar `knowledgeItemId` como `KNOWLEDGE_ID`.

> **Nota:** Si no hay API key de OpenAI, el status queda en `Pending` y `chunksCreated` será 0 (NullEmbeddingService).

### Test 5.2: Ingestar con título vacío

```http
POST /api/v1/knowledge
Authorization: Bearer <TOKEN_USER>
Content-Type: application/json

{
  "title": "",
  "originalContent": "Contenido válido",
  "type": 0
}
```

**Esperado:** `400 Bad Request` con error de validación.

### Test 5.3: Listar knowledge items

```http
GET /api/v1/knowledge
Authorization: Bearer <TOKEN_USER>
```

**Esperado:** `200 OK` — lista con al menos el item creado en 5.1.

### Test 5.4: Buscar por semántica (requiere API key de OpenAI)

```http
GET /api/v1/knowledge/search?q=como%20manejar%20errores%20en%20csharp&maxResults=5
Authorization: Bearer <TOKEN_USER>
```

**Esperado:** `200 OK`
```json
{
  "results": [
    {
      "knowledgeItemId": "<GUID>",
      "title": "Guía de Result Pattern en C#",
      "chunkText": "...",
      "score": 0.8,
      "type": 0,
      "sourceUrl": "https://example.com/result-pattern"
    }
  ],
  "totalFound": 1
}
```

> Sin API key de OpenAI, `results` estará vacío (NullEmbeddingService retorna embeddings nulos).

### Test 5.5: Eliminar knowledge item

```http
DELETE /api/v1/knowledge/<KNOWLEDGE_ID>
Authorization: Bearer <TOKEN_USER>
```

**Esperado:** `204 No Content`.

### Test 5.6: Eliminar knowledge item inexistente

```http
DELETE /api/v1/knowledge/00000000-0000-0000-0000-000000000000
Authorization: Bearer <TOKEN_USER>
```

**Esperado:** `404 Not Found`.

---

## Fase 6 — Repositorios y Code Guardian

### Test 6.1: Registrar repositorio

```http
POST /api/v1/repositories
Authorization: Bearer <TOKEN_USER>
Content-Type: application/json

{
  "repositoryUrl": "https://github.com/dotnet/aspnetcore",
  "trust": 1
}
```

**Esperado:** `201 Created`
```json
{
  "repositoryId": "<GUID>",
  "repositoryUrl": "https://github.com/dotnet/aspnetcore"
}
```
> Guardar `repositoryId` como `REPO_ID`. Nota: para una prueba más rápida, usar un repo chico.

### Test 6.2: Listar repositorios

```http
GET /api/v1/repositories
Authorization: Bearer <TOKEN_USER>
```

**Esperado:** `200 OK` con el repositorio registrado.

### Test 6.3: Conectar a SignalR de análisis

```
URL: wss://localhost:7242/hubs/analysis?access_token=<TOKEN_USER>
```

Invocar:
```json
{ "type": 1, "target": "JoinRepository", "arguments": ["<REPO_ID>"] }
```

### Test 6.4: Disparar análisis

```http
POST /api/v1/repositories/<REPO_ID>/analyze
Authorization: Bearer <TOKEN_USER>
```

**Esperado:**
- HTTP: `202 Accepted` con header `Location: /api/v1/repositories/<REPO_ID>/reports`
- SignalR: recibir `ReceiveProgress(percent, status)` periódicamente, luego `ReceiveComplete(reportId)` o `ReceiveError(message)`

> **Importante:** El análisis clona el repo, así que puede tardar dependiendo del tamaño. Usar un repo chico para QA.

### Test 6.5: Listar reportes del repositorio

```http
GET /api/v1/repositories/<REPO_ID>/reports
Authorization: Bearer <TOKEN_USER>
```

**Esperado:** `200 OK` — al menos 1 reporte si el análisis terminó.

### Test 6.6: Ver detalle de reporte

```http
GET /api/v1/repositories/reports/<REPORT_ID>
Authorization: Bearer <TOKEN_USER>
```

**Esperado:** `200 OK`
```json
{
  "id": "<GUID>",
  "status": "Completed",
  "summary": "...",
  "totalFindings": <n>,
  "criticalFindings": <n>,
  "completedAt": "...",
  "findings": [
    {
      "severity": "Info|Warning|Error|Critical",
      "category": "...",
      "message": "...",
      "filePath": "...",
      "lineNumber": 42
    }
  ]
}
```

---

## Fase 7 — Tendencias (público, sin auth)

### Test 7.1: Listar tendencias

```http
GET /api/v1/trends
```

**Esperado:** `200 OK` — lista de tendencias tecnológicas. Puede estar vacía si el TrendScanner no corrió todavía.

> El TrendScanner corre automáticamente al iniciar la app y luego cada 6 horas. Si hay API key de Anthropic configurada, debería haber datos después de unos minutos.

### Test 7.2: Filtrar por categoría

```http
GET /api/v1/trends?category=0
```

**Esperado:** `200 OK` — solo tendencias de tipo `Framework` (category=0).

Categorías: `Framework=0`, `Language=1`, `Tool=2`, `Pattern=3`, `Platform=4`, `Library=5`.

### Test 7.3: Ver snapshots de una tendencia

```http
GET /api/v1/trends/<TREND_ID>/snapshots
```

**Esperado:** `200 OK` — lista de snapshots históricos con score, dirección y fecha.

---

## Fase 8 — Administración (requiere rol Admin)

> Usar `TOKEN_ADMIN` para estos tests.

### Test 8.1: Listar publish requests (admin)

```http
GET /api/v1/admin/publish-requests
Authorization: Bearer <TOKEN_ADMIN>
```

**Esperado:** `200 OK` — lista (posiblemente vacía si nadie pidió publicar).

### Test 8.2: Acceso denegado con usuario normal

```http
GET /api/v1/admin/publish-requests
Authorization: Bearer <TOKEN_USER>
```

**Esperado:** `403 Forbidden`.

### Test 8.3: Revisar publish request (cuando haya uno)

```http
POST /api/v1/admin/publish-requests/<REQUEST_ID>/review
Authorization: Bearer <TOKEN_ADMIN>
Content-Type: application/json

{
  "approved": true,
  "rejectionReason": null
}
```

**Esperado:** `204 No Content`.

### Test 8.4: Rechazar publish request

```http
POST /api/v1/admin/publish-requests/<REQUEST_ID>/review
Authorization: Bearer <TOKEN_ADMIN>
Content-Type: application/json

{
  "approved": false,
  "rejectionReason": "El contenido no es relevante para el knowledge base compartido"
}
```

**Esperado:** `204 No Content`.

---

## Fase 9 — Tests Automatizados

### Unit Tests (sin dependencias externas)

```bash
dotnet test tests/SentientArchitect.UnitTests
```

**Esperado:** 36 tests pasan. Cubren:
- Entidades de dominio (KnowledgeItem, Conversation, RepositoryInfo)
- Result pattern
- Use cases con mocks (IngestKnowledge)

### Integration Tests (requieren Docker)

```bash
dotnet test tests/SentientArchitect.IntegrationTests
```

**Esperado:** 7 tests pasan. Requieren Docker corriendo (Testcontainers levanta un PostgreSQL automáticamente). Cubren:
- Persistencia de KnowledgeItem (CRUD, transiciones de estado, enums como string)
- Persistencia de Conversation (con mensajes, archivado, filtro por usuario)

---

## Fase 10 — Verificaciones Generales

### 10.1: Health check básico

La app no tiene un endpoint `/health` dedicado. Verificar que responde:
```http
GET /api/v1/trends
```
Si devuelve `200 OK`, la app está levantada y conectada a la base de datos.

### 10.2: Swagger/Scalar disponible

Abrir en el navegador: `https://localhost:7242/scalar/v1`

**Esperado:** UI interactiva de Scalar con todos los endpoints documentados.

### 10.3: SignalR hubs accesibles

Verificar que los hubs responden al negotiate:
```http
POST /hubs/conversation/negotiate?negotiateVersion=1
Authorization: Bearer <TOKEN_USER>
```

**Esperado:** `200 OK` con información de transporte disponible.

### 10.4: Verificar seeding de admin

Si `Seeder:AdminEmail` y `Seeder:AdminPassword` están configurados, el login con esas credenciales debe funcionar (Test 1.6).

---

## Notas Importantes

1. **CORS no configurado:** No hay configuración explícita de CORS. Si se prueba desde un frontend en otro origen, los requests van a ser bloqueados. Esto no afecta pruebas desde Postman/curl.

2. **Degradación silenciosa de IA:** Sin API keys, la app NO crashea — simplemente las funcionalidades de IA no hacen nada. Verificar los logs si algo parece no funcionar.

3. **Puertos:** HTTP 5291, HTTPS 7242. Si cambian, revisar `Properties/launchSettings.json`.

4. **pgvector requerido:** PostgreSQL debe tener la extensión `vector` instalada. La imagen `pgvector/pgvector:pg16` la incluye.

5. **Análisis de repos:** El análisis es estático — NUNCA ejecuta código del repositorio analizado. Solo clona, parsea y analiza archivos.
