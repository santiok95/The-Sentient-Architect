# Radar Agent — Plan de Accion

> Fecha: 2026-04-23
> Rama: `feature/consultant-radar`
> Estado: plan acordado, sin implementacion aun

---

## Objetivo

Sumar un **tercer tipo de agente conversacional** llamado **Radar**, disponible en el dropdown de "Nueva consulta" al lado de Knowledge y Consultant.

El Radar se diferencia del Knowledge en tres cosas:

1. **Fuente primaria**: consulta `TechnologyTrends` (datos del Trends Scanner), no la base de conocimiento del usuario.
2. **Fuente secundaria de validacion**: usa el Brain (SearchPlugin) **solo para verificar coherencia** — si el trend que recomienda contradice una regla ya documentada por el usuario, lo dice explicitamente.
3. **Atribucion transparente**: distingue visualmente en la respuesta que vino del radar vs que vino del brain — el usuario nunca tiene que adivinar el origen de una afirmacion.

---

## Motivacion — por que existe este plan

En el Consultant actual, cuando el usuario pregunta algo del tipo "segun Trends, que se esta usando en Testing Automation", el LLM:

- tiene el `TrendsPlugin` registrado y una regla en el system prompt que dice "usalo para modernization"
- pero el trigger del prompt es estrecho: solo la palabra "modernization" activa la llamada
- y no hay regla que diga "cuando el usuario menciona 'trends', NO uses Brain"

Resultado observado en produccion: el LLM llamo a `Search-SearchByMeaning` (Brain), **no llamo** a `Trends-GetRelevantTrends`, y **etiqueto los resultados del Brain como si fueran Trends** agregando la palabra "Trend:" al inicio de cada item. Eso es alucinacion de atribucion — la peor clase, porque el usuario cree que le estas dando info actualizada del ecosistema cuando en realidad es documentacion interna re-etiquetada.

El plan a futuro (mencionado por el usuario) es darle un **select en la UI para que el usuario elija explicitamente** Brain vs Trends. El Radar Agent es la primera mitad de esa solucion: separar el flujo de ejecucion a nivel de agente, para que el ruteo deje de depender de que el LLM adivine bien.

---

## Alcance — que SI entra y que NO entra en esta feature

### IN SCOPE

- Nuevo valor `AgentType.Radar` en el enum del backend.
- Nueva `RadarAgentFactory` con plugins `TrendsPlugin` + `SearchPlugin`.
- Nuevo system prompt especifico para Radar.
- Nuevo flujo `RunDeterministicRadarFlowAsync` en `ChatExecutionService`.
- Tercer item en el dropdown de "Nueva consulta" en el frontend.
- Schemas de frontend actualizados (`z.enum`, type union).
- Tests unitarios del routing por `AgentType` en `ExecuteChatUseCase` (si existen).

### OUT OF SCOPE

- No se agregan plugins nuevos (usa los que ya existen).
- No se agrega UI de configuracion del agente (no tiene `ContextMode` como el Consultant).
- No se agrega repositorio binding (Radar no depende de un repo concreto).
- No se toca el background `TrendScannerService` ni el endpoint `/api/v1/admin/trends/sync`.
- No se agrega migration (el enum se guarda como string varchar(50), el nuevo valor entra sin cambiar el schema).
- No se implementa rate limit por agente (quedo registrado como mejora futura en `SECURITY_BACKLOG.md`).

---

## Precondicion no-negociable — data en TechnologyTrends

**Si la tabla `TechnologyTrends` tiene menos de ~20 registros relevantes y actualizados, este feature va a dar respuestas flacas** y el LLM va a estar tentado de rellenar con Brain (exactamente el problema que queremos evitar).

### Accion previa obligatoria

Antes de empezar con el codigo, correr:

```sql
SELECT
  "Direction",
  COUNT(*) AS cantidad,
  MAX("CreatedAt") AS ultimo_scan
FROM "TechnologyTrends"
GROUP BY "Direction";
```

**Criterio para arrancar**:
- Al menos 20 trends con `Direction = Rising` o `Stable`.
- El `ultimo_scan` debe ser menor a 7 dias.

**Si no se cumple**:
- Correr `POST /api/v1/admin/trends/sync` manualmente varias veces para poblar.
- Solo despues de tener data suficiente, arrancar con la implementacion.

---

## Fases de implementacion

### Fase 1 — Backend: Enum + Factory (15 min)

**Archivos a tocar**:

- `apps/backend/SentientArchitect.Domain/Enums/AgentType.cs`
  - Agregar: `Radar = 2,`
  - Confirmar que no hay otro lugar que use un switch exhaustivo sobre este enum sin caso default (grep previo recomendado).

- `apps/backend/SentientArchitect.Infrastructure/Agents/RadarAgentFactory.cs` *(NUEVO)*
  - Copy-pattern de `KnowledgeAgentFactory.cs`.
  - Registrar **solo** estos plugins:
    - `TrendsPlugin` bajo el namespace `"Trends"`.
    - `SearchPlugin` bajo el namespace `"Search"` (para validacion contra Brain).
  - NO registrar `IngestPlugin`, NO registrar `ProfilePlugin`, NO registrar `SummaryPlugin`, NO registrar `RepositoryContextPlugin`.

- `apps/backend/SentientArchitect.Infrastructure/InfrastructureServiceExtensions.cs`
  - Registrar `RadarAgentFactory` como singleton.

**Por que singleton**: las factories actuales (Consultant y Knowledge) ya estan como singleton en DI. Seguir la misma convencion.

**Riesgo**: si en algun lado del codigo hay un `switch` sobre `AgentType` sin `default:` o sin `_ =>`, el compilador puede alertar. Resolver ese punto ahi mismo.

---

### Fase 2 — Backend: System prompt del Radar (1-2h)

**Archivo a tocar**:

- `apps/backend/SentientArchitect.Infrastructure/Chat/ChatExecutionService.cs`
  - Agregar una constante `RadarSystemPrompt` al lado de `KnowledgeSystemPrompt` y `ConsultantSystemPrompt`.

**Principios del prompt** (no el texto final — eso se afina iterando):

1. **Fuente primaria explicita**: "Tu fuente principal es `Trends-GetRelevantTrends`. SIEMPRE llamala primero."

2. **Brain solo como validador**: "Solo llamas a `Search-SearchByMeaning` despues de tener los trends, y el unico proposito es verificar si algun trend que vas a recomendar contradice una regla documentada en el knowledge base del usuario."

3. **Atribucion obligatoria por linea**: "Cada afirmacion debe ir prefijada por origen. Usa `[Radar]` para info de trends y `[Brain]` para info de la base de conocimiento. Nunca mezcles ambas en una misma oracion sin marcar la transicion."

4. **Conflicto explicito**: "Si un trend contradice una regla del Brain, escribe una seccion `## Conflicto detectado` con:
   - el trend recomendado por el radar
   - la regla del brain que contradice
   - tu recomendacion final y por que"

5. **Falta de datos honesta**: "Si el `TrendsPlugin` devuelve vacio o muy pocos resultados, decilo literal: 'El radar no tiene data suficiente sobre este tema. Considera correr un scan manual.' NO rellenes con Brain para compensar."

6. **Formato de respuesta**:
   - `## Resumen` (2-3 lineas)
   - `## Trends detectados` (bullets con formato `[Radar] nombre — direccion — por que importa`)
   - `## Validacion contra tu proyecto` (opcional, solo si hubo matches en Brain)
   - `## Conflicto detectado` (opcional, solo si los hay)
   - `## Fuentes` (igual que el Consultant — markdown links con `SourceUrl` del Brain **solo** si se uso Brain en esta respuesta)

**Criterio de exito del prompt**: con un `TechnologyTrends` poblado, preguntar "que hay de nuevo en Testing Automation?" y que la respuesta **no cite ningun item del Brain** si el Brain no tiene nada relevante. Cero alucinacion de atribucion.

---

### Fase 3 — Backend: Flujo de ejecucion (1h)

**Archivo a tocar**:

- `apps/backend/SentientArchitect.Infrastructure/Chat/ChatExecutionService.cs`

**Cambios**:

1. **Inyectar** `RadarAgentFactory radarFactory` en el constructor primario.

2. **Refactor del ruteo**: hoy es binario:
   ```csharp
   var isConsultant = request.AgentType == AgentType.Consultant;
   var kernel = isConsultant ? consultantFactory.CreateKernel(services) : knowledgeFactory.CreateKernel(services);
   ```
   Pasar a switch expresion:
   ```csharp
   var kernel = request.AgentType switch
   {
       AgentType.Consultant => consultantFactory.CreateKernel(services),
       AgentType.Radar      => radarFactory.CreateKernel(services),
       _                    => knowledgeFactory.CreateKernel(services),
   };
   ```

3. **Agregar** `RunDeterministicRadarFlowAsync`:
   - Copy-pattern de `RunDeterministicKnowledgeFlowAsync` (el mas simple — no tiene context mode ni repository priority).
   - Diferencia clave: en vez de llamar a `searchPlugin.SearchByMeaningAsync` **primero** (como hace Knowledge), el Radar deja que el LLM decida el orden. Confiamos en el system prompt para que llame a Trends antes que a Search.

4. **Actualizar el dispatch**:
   ```csharp
   var result = request.AgentType switch
   {
       AgentType.Consultant => await RunDeterministicConsultantFlowAsync(...),
       AgentType.Radar      => await RunDeterministicRadarFlowAsync(...),
       _                    => await RunDeterministicKnowledgeFlowAsync(...),
   };
   ```

**Cuidado**: el chatHistory se construye con system prompt distinto segun el agente. Mantener la logica actual de `BuildChatHistory(history, promptSeleccionado)`.

---

### Fase 4 — Backend: Contratos y validaciones (15 min)

**Archivos a revisar**:

- `apps/backend/SentientArchitect.Application/Features/Conversations/CreateConversation/CreateConversationRequest.cs`
  - Si hay validacion del `AgentType` contra valores permitidos (ej. lista blanca hardcodeada), actualizar.

- `apps/backend/SentientArchitect.Application/Features/Conversations/Chat/ChatExecutionContracts.cs`
  - Verificar que el DTO acepta el nuevo valor.

- Cualquier validator de FluentValidation o check manual que enumere los agentes aceptados.

**Importante**: no todos los lugares usan el enum directo — algunos reciben string y parsean. Ahi hay que revisar que el parse del nuevo `"Radar"` funcione.

---

### Fase 5 — Frontend: Schemas y tipos (15 min)

**Archivos a tocar**:

- `apps/frontend/src/lib/schemas.ts`
  - Linea 69: `z.enum(['Knowledge', 'Consultant'])` → `z.enum(['Knowledge', 'Consultant', 'Radar'])`
  - Linea 127: `export type AgentType = 'Knowledge' | 'Consultant'` → agregar `| 'Radar'`

- `apps/frontend/src/lib/schemas.test.ts`
  - Si hay tests del enum, sumar caso para `Radar`.

- `apps/frontend/src/features/consultant/hooks/useConversations.ts`
  - Linea 11: `agentType: 'Knowledge' | 'Consultant'` → agregar `| 'Radar'`

- Cualquier otro archivo que tenga el union hardcodeado (grep `'Knowledge' | 'Consultant'`).

---

### Fase 6 — Frontend: UI del dropdown (15 min)

**Archivo a tocar**:

- `apps/frontend/src/features/consultant/components/ConversationList.tsx`

**Cambios** (ConversationList.tsx:327-344):

- Agregar un tercer `DropdownMenuItem` despues del Consultant:
  ```tsx
  <DropdownMenuItem
    className="gap-2 text-sm"
    onClick={() => onCreateConversation('Radar')}
  >
    <Radar className="h-4 w-4 text-muted-foreground" />
    <div>
      <p className="font-medium">Radar</p>
      <p className="text-xs text-muted-foreground">Tendencias y validacion</p>
    </div>
  </DropdownMenuItem>
  ```

- Importar el icono `Radar` de `lucide-react` (ya existe en la libreria).

- NO llamar a `handleConsultantClick` (Radar no requiere picker de repo) — llamada directa a `onCreateConversation('Radar')`.

**Verificacion visual**: que el dropdown muestre los 3 items en el orden: Knowledge, Consultant, Radar.

---

### Fase 7 — Frontend: Rendering de mensajes (30 min)

**Archivo a revisar**:

- `apps/frontend/src/features/consultant/components/ChatPanel.tsx`

**Por que revisarlo**: el Radar va a devolver respuestas con marcadores `[Radar]` y `[Brain]` inline. Necesitamos confirmar que:

1. El renderer de markdown no los rompe.
2. (Opcional, nice-to-have) Podemos darles estilo visual distinto — ej. badge de color diferente segun la fuente.

**Si es complejo, dejarlo para una segunda iteracion**. En la v1 alcanza con que se vea como texto plano con los marcadores.

---

### Fase 8 — Testing (1h)

**Unit tests — backend**:

- `apps/backend/tests/SentientArchitect.UnitTests/Application/Features/Conversations/ExecuteChatUseCaseTests.cs`
  - Sumar test: "ExecuteAsync_RoutesToRadarFactory_WhenAgentTypeIsRadar".
  - Mockear las tres factories y verificar que se llama la correcta.

**Smoke test manual** (obligatorio antes de mergear):

1. Crear conversacion Radar desde el dropdown.
2. Preguntar: **"Que tendencias hay en Testing Automation?"**
   - Esperado: respuesta menciona items de `TechnologyTrends` (ej. Playwright, Testcontainers, etc.) con `[Radar]` prefix.
   - Esperado: si el Brain tiene algo relevante, aparece `## Validacion contra tu proyecto` con `[Brain]`.
   - No esperado: citar items del Brain como si fueran trends.

3. Preguntar: **"Que recomendas para mi stack?"**
   - Esperado: responde algo tipo "No tengo contexto de tu stack. Cambia al Consultant para una respuesta personalizada."
   - Validacion: el Radar NO tiene `ProfilePlugin` — no debe inventar un perfil.

4. Preguntar algo que **el radar no tenga**: **"Que opinas de COBOL?"**
   - Esperado: "El radar no tiene data suficiente sobre este tema."
   - No esperado: alucinacion rellenando con Brain o con conocimiento general del LLM.

5. Preguntar algo que genere **conflicto brain-vs-radar**:
   - Setup: que el Brain tenga una regla "no usamos Repository pattern" y que el radar diga "Repository pattern esta en auge".
   - Esperado: seccion `## Conflicto detectado` explicita.

**Criterio de mergeable**: los 5 casos pasan sin intervencion manual.

---

### Fase 9 — Documentacion (15 min)

**Archivos a actualizar**:

- `docs/IMPLEMENTATION_LOG.md` — entrada nueva con fecha + scope + commits.
- `docs/API_CONTRACTS.md` — actualizar la seccion de `AgentType` si lista los valores.
- `CLAUDE.md` raiz — en la seccion "Agents (Semantic Kernel)" sumar Radar como agente conversacional.
- `.claude/rules/semantic-kernel.md` — si hay una seccion que enumera los agentes, sumar Radar.

---

## Orden de ejecucion sugerido (minimiza retrabajo)

```
Fase 1 (enum + factory)
   ↓
Fase 3 (flujo de ejecucion)   ← sin prompt todavia, con un prompt stub
   ↓
Fase 4 (contratos)            ← el backend ya responde al AgentType=Radar
   ↓
Fase 5 + Fase 6 (frontend)    ← el flujo end-to-end ya funciona
   ↓
Fase 2 (system prompt real)   ← iteramos el prompt con el flujo completo armado
   ↓
Fase 7 (rendering)
   ↓
Fase 8 (testing)
   ↓
Fase 9 (docs)
```

**Por que este orden**: el prompt (Fase 2) es lo que **mas tiempo va a consumir iterando**. Tenerlo al final — con todo el pipeline funcionando — permite probar prompts reales contra respuestas reales. Si lo hacemos primero, vamos a estar editandolo a ciegas.

---

## Riesgos identificados

| # | Riesgo | Mitigacion |
|---|--------|-----------|
| 1 | `TechnologyTrends` vacia o desactualizada | Precondicion obligatoria antes de Fase 1. |
| 2 | LLM ignora el system prompt y llama a Search antes que Trends | Fase 8 smoke test caso 1 lo detecta. Si pasa, endurecer el prompt con `FunctionChoiceBehavior` configurado para forzar el primer call. |
| 3 | El LLM inventa prefijos `[Radar]` sobre info que saco del Brain | Smoke test caso 4. Si pasa, agregar al prompt una regla "NUNCA prefijes con [Radar] info que no vino de `Trends-GetRelevantTrends`". |
| 4 | El `Radar` icon de lucide-react no existe en la version actual | Fallback: usar `Satellite` o `Activity`. Verificar con `grep "export.*Radar" node_modules/lucide-react`. |
| 5 | Tests de `ExecuteChatUseCaseTests` asumen 2 agentes con un boolean | Refactor del test a switch antes de sumar el caso Radar. |
| 6 | Un usuario abre una conversacion Radar vieja despues de que hayamos cambiado el prompt | Las conversaciones persistidas guardan `agentType` como string, el routing sigue funcionando. El prompt aplicado es el actual (no el del momento de creacion) — aceptable. |

---

## Estimacion total

**3-4 horas de laburo concentrado**, distribuidas asi:

- Codigo mecanico (Fases 1, 3-7): ~1.5h
- System prompt iterando (Fase 2): ~1-2h
- Testing (Fase 8): ~1h
- Docs (Fase 9): ~15 min

El 50% del tiempo real se va en **afinar el prompt y validar que no alucina atribucion**. Es lo dificil y lo que menos se ve.

---

## Definicion de "Listo" (DoD)

La feature se considera terminada cuando:

- [ ] Precondicion: `TechnologyTrends` tiene ≥20 items Rising/Stable con scan <7 dias.
- [ ] Backend compila sin warnings.
- [ ] Frontend compila sin type errors.
- [ ] Dropdown de "Nueva consulta" muestra 3 opciones.
- [ ] Se puede crear, listar, chatear y archivar una conversacion Radar.
- [ ] Los 5 casos del smoke test (Fase 8) pasan.
- [ ] Respuestas del Radar distinguen visualmente `[Radar]` vs `[Brain]`.
- [ ] Cuando Trends esta vacio, el Radar lo dice en vez de rellenar.
- [ ] `IMPLEMENTATION_LOG.md` actualizado.
- [ ] Commit squashed con mensaje convencional: `feat: add Radar agent for trends-focused conversations`.

---

## Decisiones pendientes (para discutir antes de codear)

1. **Icono del Radar en el dropdown**: `Radar` o `Satellite`? Ambos existen en lucide-react. `Radar` es mas directo.

2. **Scope del SearchPlugin en Radar**: queremos que vea personal + shared (como hoy), solo shared, o todo (admin)? Para v1 dejar igual que Consultant/Knowledge.

3. **FunctionChoiceBehavior**: dejar `Auto` (actual) o forzar con `Required` que el primer call sea a Trends? `Required` da garantia pero quita flexibilidad. Empezar con `Auto` y subir a `Required` solo si el smoke test falla.

4. **Primer deploy**: mergear directo a main o pasar por staging? No hay staging hoy — por eso la Fase 8 smoke test es no-negociable.
