# Registro De Agentes De Auditoria

Esta carpeta contiene definiciones reutilizables de agentes **solo de auditoria** para revisiones estructuradas. Estos agentes estan pensados para **diagnosticar, puntuar y recomendar**. No deben implementar features ni modificar codigo de producto durante una auditoria.

## Agentes disponibles

| Agente | Alcance principal | Usalo cuando necesites |
|---|---|---|
| [frontend-agent-final.md](frontend-agent-final.md) | Next.js, React, TypeScript, UX | Una revision enfocada de arquitectura cliente, calidad de UI, accesibilidad y performance frontend |
| [backend-agent-final.md](backend-agent-final.md) | .NET, APIs, datos, seguridad | Una auditoria estricta de limites arquitectonicos, calidad de API, acceso a datos y preparacion operativa |
| [chatbot-prompt-agent-final.md](chatbot-prompt-agent-final.md) | Flujos LLM, prompts, tools | Una revision de confiabilidad conversacional, seguridad de prompts, grounding y disciplina de tokens |
| [chief-auditor-agent-final.md](chief-auditor-agent-final.md) | Sintesis transversal | Un veredicto ejecutivo que consolida las auditorias especializadas |

## Uso sugerido

1. Ejecuta primero el agente especializado que corresponda al area bajo revision.
2. Usa al auditor jefe al final para consolidar hallazgos, causas raiz y prioridades.
3. Abri una tarea separada de implementacion solo despues de terminar la auditoria.

Tambien podes usar al chief auditor primero como punto de entrada, pero con una condicion importante: debe trabajar en **modo orquestador**. Eso significa que no deberia inventar un analisis global por su cuenta ni hacer una revision superficial de todo. Primero tiene que disparar o pedir las tres auditorias especializadas y, recien con esos resultados, producir la sintesis general.

## Mejor forma de usarlos

### Orden recomendado

Para una auditoria completa del sistema, usalos en esta secuencia:

1. **Frontend Agent Final**
   Revisa la app Next.js, la arquitectura de componentes, el flujo de estado, la performance, la accesibilidad y la mantenibilidad.
   Es el indicado cuando queres saber si la capa de UI esta lista para produccion o si se esta volviendo fragil.

2. **Backend Agent Final**
   Revisa arquitectura .NET, limites entre servicios, acceso a datos, seguridad, resiliencia y preparacion para produccion.
   Es el indicado cuando necesitas un veredicto tecnico serio sobre la API y el diseño backend.

3. **Chatbot & Prompt Agent Final**
   Audita comportamiento del agente, prompts, memoria, orquestacion de tools, seguridad, latencia y costo operativo.
   Es el indicado cuando importa la calidad conversacional o la confiabilidad del sistema LLM.

4. **Chief Auditor Agent Final**
   Usalo solo cuando ya existan los reportes anteriores.
   Su trabajo es consolidar hallazgos, detectar causas raiz y ordenar prioridades ejecutivas.

   Alternativa valida:
   tambien lo podes invocar primero si queres que funcione como orquestador. En ese caso, el flujo correcto es:
   1. lanzar frontend, backend y chatbot/prompt
   2. reunir esos tres informes
   3. devolver el veredicto general

   Si el entorno donde lo corras no soporta invocar otros agentes desde adentro, entonces el chief no puede hacerlo solo y te tiene que pedir esos tres insumos antes de consolidar.

### Flujos practicos

#### 1. Auditoria rapida y focalizada

Usa un solo agente especializado cuando el problema este claramente aislado.

Ejemplos:
- frontend inestable o dificil de mantener: usa el auditor frontend
- problemas de API, arquitectura o datos: usa el auditor backend
- inconsistencia conversacional, prompt drift o problemas con tools: usa el auditor de chatbot y prompts

#### 2. Auditoria tecnica completa

Usa primero los tres agentes especializados y al final el chief auditor.

Esto te da:
- una revision detallada por dominio
- mejor relacion señal-ruido
- un veredicto transversal sin mezclar problemas demasiado temprano

#### 3. Revision pre-produccion

Es el modo mas fuerte antes de un release.

Objetivo recomendado:
- identificar riesgos criticos
- detectar bloqueantes de produccion
- separar lo urgente de lo postergable
- evitar subir deuda arquitectonica escondida

### Consejos para pedir una buena auditoria

Para obtener mejores resultados:

- defini bien el alcance a revisar
- pedi solo hallazgos sustentados en evidencia
- pedi clasificacion por severidad
- pedi separar lo confirmado de la inferencia razonable
- no mezcles auditoria e implementacion en el mismo paso

Patron de uso recomendado:
- primero pedi diagnostico
- despues pedi priorizacion
- recien despues abri una tarea aparte de implementacion

### Nota importante

Estos agentes son **auditores**, no constructores.

Usalos para:
- detectar riesgos
- entender debilidades arquitectonicas
- priorizar deuda tecnica
- decidir donde conviene enfocar al equipo primero

No los uses como generadores de features o bots de refactor completo.
