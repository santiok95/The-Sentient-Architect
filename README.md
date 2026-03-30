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

## License

_TBD_
