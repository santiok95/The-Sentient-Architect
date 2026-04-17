# Next Steps Execution Plan

> Fecha: 2026-04-17
> Estado: plan acordado, sin implementacion aun
> Rama de trabajo sugerida: `feature/next-steps`

---

## Objetivo del documento

Este documento consolida lo que se acordo despues del audit tecnico y de la recontextualizacion posterior.

No es un backlog generico.
Es una guia de ejecucion por fases para:

- atacar primero lo mas critico
- respetar el orden logico para evitar retrabajo
- mantener compatibilidad con frontend y backend durante la remediacion
- preservar el contexto refinado para futuras auditorias y para el orquestador

---

## Contexto refinado que debe preservarse

### 1. Credenciales en `appsettings`

El repo contiene valores con forma de secreto.

Contexto aclarado por el usuario:
- no se considera incidente confirmado de credenciales reales expuestas
- esos valores fueron usados como moldes/ejemplos
- futuras auditorias no deben tratarlo como compromiso confirmado salvo nueva evidencia

### 2. Multi-tenancy real por grupo de trabajo

El objetivo del producto es tener `TenantId` por grupo/equipo, con asignacion desde registro o desde administracion.

Contexto aclarado por el usuario:
- esto no formaba parte del bloque urgente del MVP
- el esqueleto actual existe como incremento posterior
- no debe clasificarse como bug critico inmediato mientras esa capacidad no este activa de verdad

### 3. FluentValidation

Existe infraestructura base, pero no es una prioridad inmediata.

Contexto aclarado por el usuario:
- se usaba principalmente para limites de texto y validaciones chicas
- no se considera bloqueante en esta etapa

### 4. Auth y homogeneidad arquitectonica

El problema detectado no es que auth sea una mala practica por definicion.
El problema es que rompe la homogeneidad del proyecto.

El backend muestra un patron dominante:

`endpoint fino -> use case -> result`

Pero `AuthEndpoints` mezcla:

- transporte HTTP
- reglas de sesion
- consulta de identidad
- emision/refresh de tokens
- decisiones de seguridad

Eso no es necesariamente un bug puntual.
Es deuda de gobierno tecnico.

---

## Decisiones ya aceptadas

### CORS

Queda aceptado:

- permitir solo origenes explicitos
- en no produccion, habilitar solo localhost necesarios
- incluir `https://ai-lab.santiagoniveyro.online`
- no volver a `allow any origin + credentials`

### Auth / session target

Queda aceptado:

- mantener el concepto de `refresh token`
- `access token` objetivo de 2 horas
- `refresh token` en cookie `httpOnly`
- `secure` y `sameSite` segun despliegue real
- sesiones revocables en servidor
- `logout` real
- refresh rotation con deteccion de reuse

### Jobs de analisis pesados

Queda aceptado:

- priorizar persistencia y control antes que velocidad de cola
- elegir jobs respaldados por PostgreSQL antes que Redis
- sacar `Task.Run` desde endpoints como objetivo de arquitectura

### Rate limiting / proteccion de costo

Queda aceptado:

- auth: limite por IP y por identificador de login
- chat: limite por usuario y tenant, mas control de concurrencia
- analisis de repositorios: cola + concurrencia maxima
- endpoints costosos con IA: cuota y throttling por usuario/tenant, no solo por IP

---

## Objetivo arquitectonico para flujos criticos

Para zonas criticas del sistema, el criterio target es:

- API: transporte HTTP solamente
- Application: regla de negocio, autorizacion de operacion, decisiones del flujo
- Infrastructure: emision de tokens, persistencia de sesion, jobs, stores de cuotas/limites, integraciones externas

### Aplicado a auth

Target conceptual:

- `RegisterUserUseCase`
- `LoginUseCase`
- `RefreshSessionUseCase`
- `LogoutSessionUseCase`

El endpoint deberia quedar reducido a:

- bind de body/query/cookies
- traduccion a request del caso de uso
- escritura/borrado de cookies
- conversion de `Result` a HTTP

### Hardcodes a migrar a configuracion/opciones

Ejemplos detectados:

- expiracion local en auth endpoint
- politicas de cookie
- limites de rate limiting
- concurrencia maxima de jobs
- timeouts y retries operativos

Regla:

si un valor expresa politica, seguridad, costo o comportamiento operativo, no deberia vivir hardcodeado dentro del endpoint.

---

## Plan por fases

La logica del orden es esta:

1. primero cerrar ambiguedad y ownership
2. despues endurecer auth sin rehacer toda la sesion
3. despues sacar jobs del request pipeline
4. despues agregar proteccion de abuso/costo
5. recien despues hacer el rediseño completo de sesion

---

## Fase 0 — Alineacion y preparacion

### Objetivo

Cerrar ambiguedades antes de tocar flujos criticos.

### Incluye

- documentar decisiones aceptadas
- dejar claro que auth converge al patron del resto del backend
- identificar hardcodes que deben salir a config/opciones
- dejar explicitado que multi-tenancy grupal no entra en el bloque urgente
- dejar explicitado que FluentValidation queda diferido

### Ejemplo de aplicacion

Caso actual:
- `AuthEndpoints` mezcla decisiones de sesion con transporte HTTP

Aplicacion en esta fase:
- no se cambia el contrato todavia
- se identifica el boundary final deseado
- se listan los valores hardcodeados que no deberian permanecer en endpoint

### Verificacion anti-regresion

- no cambia ningun endpoint publico
- no cambia el shape del response del frontend
- no cambia la forma en que login, refresh y logout son consumidos hoy

### Criterio de salida

- decisiones registradas
- alcance claro por fase
- sin cambios funcionales todavia

---

## Fase 1 — Ownership y autorizacion por recurso

### Objetivo

Cerrar accesos indebidos sin romper UI ni semantica existente.

### Incluye

- `GetRepositoryAnalysis`
- `GetRepositoryReports`
- `GetAnalysisReport`
- cualquier otro read sensible por identificador directo

### Ejemplo de aplicacion

Caso actual:
- el endpoint recibe `id` o `reportId`
- el use case busca por id directo y devuelve datos

Target en la fase:
- el endpoint agrega `UserId` al request
- el use case filtra por ownership
- la respuesta conserva el mismo contrato para el frontend cuando el recurso si corresponde al usuario

### Verificacion anti-regresion

Backend:
- tests de autorizacion por recurso
- tests de `NotFound` o `Forbidden` segun politica elegida

Frontend:
- abrir reportes propios sigue funcionando igual
- listar repositorios propios sigue funcionando igual
- ninguna pantalla necesita cambiar el endpoint ni el payload

Manual:
- login
- listado de repos
- detalle de analisis propio
- detalle de reportes propios

### Criterio de salida

- ningun read sensible por id queda expuesto solo por estar autenticado
- frontend sigue operativo sin adaptaciones de contrato

---

## Fase 2 — Endurecimiento minimo de auth

### Objetivo

Mejorar seguridad y homogeneidad sin rediseñar toda la sesion todavia.

### Incluye

- validar `IsActive` tambien en login
- endurecer validaciones actuales de refresh
- sacar hardcodes obvios del endpoint
- comenzar a encapsular auth en use cases sin romper el flujo actual del frontend

### Ejemplo de aplicacion

Caso actual:
- login resuelve password, roles y token en el endpoint
- refresh valida token con logica propia en el endpoint

Target en la fase:
- `LoginUseCase` decide si el usuario puede autenticarse
- `RefreshSessionUseCase` decide si el refresh es valido
- el endpoint queda como capa de transporte

### Verificacion anti-regresion

Backend:
- tests de login activo/inactivo
- tests de refresh valido/invalido
- tests de claims minimos esperados

Frontend:
- mismo request de login
- mismo response consumible en esta fase
- hubs y llamadas autenticadas siguen funcionando

Manual:
- login exitoso
- login con usuario inactivo
- refresh
- logout actual
- reconexion de hubs

### Criterio de salida

- auth deja de concentrar logica critica directamente en endpoint
- no se rompe el contrato publico todavia

---

## Fase 3 — Jobs persistidos y control de concurrencia

### Objetivo

Sacar `Task.Run` del request pipeline y pasar a un modelo confiable.

### Incluye

- persistencia de jobs en PostgreSQL
- worker dedicado o pipeline de procesamiento con concurrencia maxima
- estado del job persistido
- semantica de `accepted` real

### Ejemplo de aplicacion

Caso actual:
- `POST /repositories/{id}/analyze` devuelve `202`
- lanza `Task.Run`
- si el proceso cae, el trabajo puede perderse semanticamente

Target en la fase:
- el endpoint registra o encola un job persistido
- el worker lo consume desde una fuente durable
- el frontend puede seguir recibiendo progreso por los mismos canales si no cambiamos ese contrato

### Verificacion anti-regresion

Backend:
- tests de encolado
- tests de idempotencia minima
- tests de recuperacion basica tras reinicio o reproceso

Frontend:
- el trigger de analisis sigue usando el mismo endpoint
- el progreso sigue llegando por el mismo mecanismo visible

Manual:
- disparar analisis
- reiniciar proceso durante la ejecucion
- verificar que el trabajo no desaparece semanticamente

### Criterio de salida

- no quedan jobs largos lanzados con `Task.Run` desde endpoints
- el sistema puede explicar si un trabajo esta pendiente, ejecutando, completado o fallido

---

## Fase 4 — Rate limiting, cuota y proteccion de abuso

### Objetivo

Proteger auth, chat y endpoints costosos cuando la base critica ya este mas estable.

### Incluye

- rate limiting de auth por IP e identificador
- throttling de chat por usuario/tenant
- cuota y throttling de endpoints costosos con IA
- concurrencia maxima para analisis de repositorios

### Ejemplo de aplicacion

Auth:
- proteger contra brute force o spray de credenciales

Chat:
- evitar bursts o abuso costoso por usuario

Analisis:
- limitar cuantos jobs pesados pueden correr en simultaneo

### Verificacion anti-regresion

Backend:
- tests de politicas y umbrales
- tests de respuestas `429`

Frontend:
- manejo claro de `429`
- mensajes comprensibles de retry o espera

Observabilidad:
- logs y metricas de rechazos
- visibilidad de bursts y consumo

### Criterio de salida

- auth, chat y endpoints caros tienen proteccion explicita
- el sistema no acepta carga infinita ni abuso sin señalizacion

---

## Fase 5 — Rediseño completo de sesion

### Objetivo

Llegar al target acordado sin mezclarlo con fixes urgentes de fases anteriores.

### Incluye

- `access token` de 2 horas
- `refresh token` en cookie `httpOnly`
- sesiones revocables en servidor
- `logout` real
- refresh rotation con deteccion de reuse
- asociacion de refresh token a sesion/dispositivo

### Ejemplo de aplicacion

Caso actual:
- refresh reutiliza el mismo JWT

Target en la fase:
- access token y refresh token dejan de ser el mismo artefacto
- el backend pasa a controlar la verdad de la sesion
- el frontend consume una sesion mas robusta sin depender del flujo actual improvisado

### Verificacion anti-regresion

Backend:
- tests de rotacion
- tests de revocacion
- tests de reuse detection

Frontend:
- login
- refresh silencioso si aplica
- logout
- reconexion tras expiracion del access token

Manual:
- expirar access token
- refrescar sesion
- cerrar sesion
- intentar reuse de refresh comprometido

### Criterio de salida

- la sesion deja de ser un parche y pasa a ser un flujo consistente con el target acordado

---

## Verificacion transversal por fase

En todas las fases se debe verificar esto antes de dar un cambio por bueno:

### Backend

- que compile sin nuevos errores del cambio local
- que los tests relevantes de la fase pasen
- que no se alteren contratos HTTP fuera del alcance de la fase

### Frontend

- que los endpoints usados por la UI no cambien de shape sin fase dedicada
- que login, lectura de reportes y flujos visibles sigan funcionando segun la fase
- que los errores nuevos esten controlados y no silencien la UI

### Integracion

- smoke test manual del flujo tocado
- validar que SignalR, auth y llamadas fetch no queden desalineadas
- validar que los cambios de backend no obliguen a rehacer frontend antes de tiempo

---

## Fuera de alcance inmediato

Por ahora no entra en el bloque urgente:

- multi-tenancy completo por grupo de trabajo
- adopcion seria de FluentValidation
- refactors esteticos o puramente de ordenamiento
- mejoras no criticas de UX si no estan ligadas a auth, ownership o jobs

---

## Principio de ejecucion

Primero se corrige lo que puede exponer datos o romper operacion.

Despues se homogeniza la arquitectura de auth.

Despues se corrige la infraestructura de jobs y proteccion de costo.

Recien despues se hace el rediseño completo de sesion y los incrementos no urgentes.