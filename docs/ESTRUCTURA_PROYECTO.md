# Estructura del Proyecto para Claude Code

## Estructura Completa

```
The Sentient Architect/              ← Carpeta raíz del proyecto
│
├── CLAUDE.md                        ← ARCHIVO MÁS IMPORTANTE
│                                      Claude Code lo lee automáticamente al inicio de cada sesión.
│                                      Instrucciones concisas: stack, comandos, convenciones, reglas.
│                                      MÁXIMO 100-200 líneas. Conciso y directo.
│
├── .claude/                         ← Configuración de Claude Code
│   ├── settings.json                ← Hooks, permisos, configuración
│   ├── settings.local.json          ← Overrides personales (gitignored)
│   │
│   ├── rules/                       ← Reglas modulares (se cargan junto con CLAUDE.md)
│   │   ├── clean-architecture.md    ← Reglas de Clean Architecture para .NET
│   │   ├── entity-framework.md      ← Convenciones de EF Core
│   │   ├── semantic-kernel.md       ← Patrones de Semantic Kernel
│   │   ├── testing.md               ← Estrategia de testing
│   │   └── security.md              ← Reglas de seguridad (análisis de repos)
│   │
│   ├── commands/                    ← Slash commands personalizados
│   │   ├── analyze-repo.md          ← /project:analyze-repo
│   │   └── run-tests.md             ← /project:run-tests
│   │
│   └── agents/                      ← Agentes personalizados (subagentes)
│       └── code-reviewer.md         ← Agente de revisión de código
│
├── docs/                            ← Documentación del proyecto (para humanos Y para Claude)
│   ├── PROJECT_CONTEXT.md           ← Visión completa, decisiones, flujos, riesgos
│   ├── ARCHITECTURE_DECISIONS.md    ← Entidades, relaciones, patrones EF Core
│   ├── API_CONTRACTS.md             ← Endpoints, request/response (cuando lo diseñemos)
│   └── IMPLEMENTATION_LOG.md        ← Progreso de implementación
│
├── src/                             ← Código fuente
│   ├── SentientArchitect.Domain/
│   ├── SentientArchitect.Application/
│   ├── SentientArchitect.Infrastructure/
│   ├── SentientArchitect.API/
│   └── SentientArchitect.sln
│
├── tests/
│   ├── SentientArchitect.UnitTests/
│   └── SentientArchitect.IntegrationTests/
│
├── .gitignore
└── README.md
```

## ¿Qué va dónde?

### CLAUDE.md (raíz) — Lo que Claude lee SIEMPRE
- Stack y versiones
- Comandos de build/test/lint
- Convenciones de código (naming, patterns)
- Reglas duras ("NUNCA ejecutes código de repos externos")
- Referencia a docs/ para contexto detallado

### .claude/rules/ — Reglas modulares por tema
- Se cargan automáticamente junto con CLAUDE.md
- Cada archivo es un tema específico
- Más fácil de mantener que un CLAUDE.md gigante

### docs/ — Contexto detallado
- Claude Code puede leer estos archivos cuando necesita más contexto
- NO se cargan automáticamente (a diferencia de CLAUDE.md y rules/)
- En CLAUDE.md ponés: "Para detalles de entidades, leé docs/ARCHITECTURE_DECISIONS.md"

### .claude/commands/ — Atajos
- Cada .md se convierte en un slash command
- Ejemplo: /project:analyze-repo ejecuta un flujo predefinido

### .claude/agents/ — Subagentes
- Agentes especializados con personalidad y reglas propias
- Ejemplo: un agente que revisa código según tus estándares
