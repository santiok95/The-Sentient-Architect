# Frontend Configuration Architecture

## Single Source of Truth Pattern

**PROBLEM (antes):**
- URLs hardcodeadas en múltiples lugares: `api-client.ts`, `signalr.ts`, `actions.ts`, mocks handlers
- Cambiar la URL requería actualizar 10+ archivos
- No soportaba inyección de variables de entorno en runtime (sólo build-time)
- Docker env vars no funcionaban porque `process.env.NEXT_PUBLIC_API_URL` se resuelve en build-time

**SOLUTION (ahora):**

### 1. **`src/lib/config.ts`** — Central Configuration Hub

```typescript
// SINGLE SOURCE OF TRUTH para todas las URLs
export const getApiBaseUrl(): string {
  // Lee window.__API_BASE_URL (inyectado en layout.tsx en RUNTIME)
  if (typeof window !== 'undefined') {
    const url = (window as any).__API_BASE_URL
    if (url) return url
  }
  // Fallback a build-time env
  if (process.env.NEXT_PUBLIC_API_URL) {
    return process.env.NEXT_PUBLIC_API_URL
  }
  return 'http://localhost:5291'
}

export const config = {
  get apiBaseUrl(): string { return getApiBaseUrl() },
  get wsBaseUrl(): string { return getWsBaseUrl() },
  signalrHubs: {
    conversation: '/hubs/conversation',
    ingestion: '/hubs/ingestion',
    analysis: '/hubs/analysis',
  }
}
```

### 2. **`src/app/layout.tsx`** — Runtime Injection

```typescript
<head>
  <script dangerouslySetInnerHTML={{
    __html: `window.__API_BASE_URL = "${process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5291'}"`
  }} />
</head>
```

Esto inyecta la variable en el HTML global, disponible en JavaScript.

### 3. **Consumption Pattern**

Todos los archivos que necesitan la URL **importan desde `config.ts`:**

#### REST API (api-client.ts):
```typescript
import { getApiBaseUrl } from './config'

const baseUrl = getApiBaseUrl()
fetch(`${baseUrl}/api/v1/knowledge`)
```

#### SignalR (signalr.ts):
```typescript
import { getApiBaseUrl, config } from './config'

const baseUrl = getApiBaseUrl()
const hubUrl = `${baseUrl}${config.signalrHubs.conversation}`
```

#### Server Actions (features/*/actions.ts):
```typescript
import { getApiBaseUrl } from '@/lib/config'

const BASE_URL = getApiBaseUrl()
fetch(`${BASE_URL}/api/v1/admin/...`)
```

#### MSW Handlers (mocks/handlers/*.ts):
```typescript
import { getApiBaseUrl } from '@/lib/config'

const BASE_URL = getApiBaseUrl()
```

#### Components (TrendsTable.tsx):
```typescript
import { getApiBaseUrl } from '@/lib/config'

fetch(`${getApiBaseUrl()}/api/v1/admin/trends/sync`)
```

## Docker Usage

### Build & Run
```bash
docker build -f apps/frontend/Dockerfile -t sentient-ui:latest apps/frontend

# Inyecta variable de entorno en RUNTIME
docker run -d \
  -p 3000:3000 \
  -e NEXT_PUBLIC_API_URL=http://api.production.com:5291 \
  sentient-ui:latest
```

### With docker-compose
```yaml
services:
  frontend:
    build:
      context: ./apps/frontend
      args:
        NEXT_PUBLIC_API_URL: ${NEXT_PUBLIC_API_URL:-http://localhost:5291}
    environment:
      - NEXT_PUBLIC_API_URL=${NEXT_PUBLIC_API_URL:-http://localhost:5291}
```

## Flow Diagram

```
Docker Container Start
    ↓
layout.tsx reads process.env.NEXT_PUBLIC_API_URL
    ↓
Inyecta en <head>: window.__API_BASE_URL = "..."
    ↓
JavaScript carga (client-side, en browser)
    ↓
getApiBaseUrl() lee window.__API_BASE_URL (RUNTIME)
    ↓
Todas las URLs se resuelven con la URL correcta
```

## Key Benefits

✅ **Single source of truth** — Una URL, un lugar  
✅ **Runtime configuration** — Funciona con Docker env vars  
✅ **No build-time coupling** — No necesita re-build para cambiar URL  
✅ **Type-safe** — config.ts es centralizado y tipado  
✅ **SignalR + REST unified** — Mismo patrón para todo  
✅ **SSR-safe** — `typeof window` checks previenen errores  

## Testing

### Local Development
```bash
# npm run dev detecta NEXT_PUBLIC_API_URL automáticamente
NEXT_PUBLIC_API_URL=http://localhost:5291 npm run dev
```

### Production
```bash
# El container inyecta la variable en runtime
docker run -e NEXT_PUBLIC_API_URL=https://api.example.com sentient-ui
```
