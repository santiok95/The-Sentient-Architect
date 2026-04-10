# The Sentient Architect

An AI-powered developer knowledge management system that unifies semantic knowledge storage, architectural consulting, code analysis, and technology trend monitoring into a single interconnected ecosystem.

## What It Does

**Semantic Brain** — Store articles, notes, documentation, and repository links. Search your knowledge base using natural language — find things by meaning, not just keywords.

**Architecture Consultant** — Get architectural advice grounded in YOUR knowledge base. Multi-turn consultations that remember your stack, patterns, and past decisions.

**Code Guardian** — Analyze repositories for quality, security, and technical debt. Two modes: full security scan for external repos, quality-focused analysis for internal code.

**Trends Radar** — Background monitoring of technology trends relevant to your stack. Track what's emerging, growing, mainstream, or declining.

## Tech Stack

- **Runtime**: .NET 9
- **Database**: PostgreSQL + pgvector (relational + vector storage in one DB)
- **AI Orchestration**: Semantic Kernel
- **Code Analysis**: Roslyn (C# static analysis)
- **Architecture**: Clean Architecture (Domain → Application → Infrastructure → Presentation)

## Project Structure

```
src/
├── SentientArchitect.Domain/          # Entities, interfaces, enums (zero dependencies)
├── SentientArchitect.Application/     # Use cases, DTOs, agent orchestration
├── SentientArchitect.Infrastructure/  # EF Core, pgvector, Semantic Kernel, external APIs
└── SentientArchitect.API/             # REST endpoints, SignalR, middleware
tests/
├── SentientArchitect.UnitTests/
└── SentientArchitect.IntegrationTests/
docs/
├── PROJECT_CONTEXT.md                 # Full project vision and decisions
├── ARCHITECTURE_DECISIONS.md          # Entity model and architecture details
└── IMPLEMENTATION_LOG.md              # Progress tracking
```

## Prerequisites

- .NET 9 SDK
- PostgreSQL 16+ with pgvector extension
- An LLM API key (OpenAI or Anthropic) for Semantic Kernel

## Setup

_Coming soon — currently in architecture and design phase._

## Documentation

See the `docs/` folder for detailed architecture decisions, entity models, and implementation progress.

## Chat Agent Routing and Context Modes

Endpoint:

```http
POST /api/v1/conversations/<CONVERSATION_ID>/chat
Authorization: Bearer <TOKEN_USER>
Content-Type: application/json
```

### `agentType`

- `Knowledge` (default): answers using retrieved project knowledge and sends content via SignalR tokens.
- `Consultant`: provides architecture guidance using user profile, conversation summary, knowledge rules, and repository context.

### `contextMode` (used by `Consultant`)

- `Auto`: if no `activeRepositoryId` and no `preferredStack`, the assistant may ask a clarification question first.
- `RepoBound`: anchors recommendations to the selected analyzed repository (`activeRepositoryId`).
- `StackBound`: prioritizes `preferredStack` and avoids forcing conventions from a different stack.
- `Generic`: keeps advice stack-agnostic and not repository-specific.

### Context Resolution Order

1. Values sent in the current request.
2. Values already persisted in the conversation.
3. Fallback defaults (`Knowledge` for `agentType`, `Auto` for `contextMode` in consultant flow).

### Example Requests

Clarification-first behavior (`Auto` with missing context):

```json
{
	"message": "Necesito diseñar un sistema de notificaciones para 100k usuarios",
	"agentType": "Consultant",
	"contextMode": "Auto"
}
```

Direct stack-constrained answer:

```json
{
	"message": "Necesito diseñar un sistema de notificaciones para 100k usuarios",
	"agentType": "Consultant",
	"contextMode": "StackBound",
	"preferredStack": "Java + Spring Boot"
}
```

## License

_TBD_
