# 🏗️ The Sentient Architect
### *El Segundo Cerebro definitivo para Arquitectos de Software.*

[![Next.js 15](https://img.shields.io/badge/Next.js-15-black?style=for-the-badge&logo=next.js)](https://nextjs.org/)
[![.NET 9](https://img.shields.io/badge/.NET-9-512bd4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-vector-336791?style=for-the-badge&logo=postgresql)](https://www.postgresql.org/)
[![React 19](https://img.shields.io/badge/React-19-61dafb?style=for-the-badge&logo=react)](https://react.dev/)

**The Sentient Architect** es una plataforma inteligente diseñada para centralizar el conocimiento técnico, auditar la salud del código y asistir en la toma de decisiones arquitectónicas en tiempo real. No es solo una herramienta, es tu socio en el diseño de sistemas de alta disponibilidad.

---

## ⚡ La Misión: ¿Qué problemas resolvemos?
En el desarrollo de software moderno, el conocimiento está fragmentado y la deuda técnica crece en las sombras. Atacamos tres dolores críticos:

*   **🧠 Fragmentación del Conocimiento (Context Loss):** Los equipos olvidan las decisiones de diseño. El *Semantic Brain* actúa como una memoria colectiva indexada semánticamente.
*   **🛡️ Arquitectura "Ciega" (Architectural Drift):** El código se desvía de los patrones originales. El *Code Guardian* detecta estas desviaciones mediante IA.
*   **📉 Fatiga de Decisiones:** Evaluar stacks consume tiempo. El *Architecture Consultant* provee una opinión experta basada en tu contexto específico.

---

## 🎯 ¿Para quién es esto?

*   **👶 Para el Junior Developer:** Es su **"Guardaespaldas Arquitectónico"**. Le permite tomar decisiones con confianza, asegurando que cada línea de código siga los patrones y estándares de diseño ya establecidos por el equipo de arquitectura. Menos dudas, más código de calidad.
*   **👴 Para el Senior Architect:** Es su **"Radar de Vanguardia"**. En un mundo donde aparece un framework nuevo cada día, el sistema actúa como un filtro inteligente. Provee snapshots curados y análisis de tracción real, permitiendo al Senior concentrarse en la estrategia mientras el sistema monitorea el ecosistema.

---

## 🧩 Los 4 Pilares del Sistema

### 1. 🧠 Semantic Brain
Motor de ingesta y búsqueda basado en **RAG (Retrieval-Augmented Generation)**.
*   Indexación semántica en **PostgreSQL + pgvector**.
*   Búsqueda híbrida que entiende el "significado" técnico, no solo palabras clave.

### 2. 🛡️ Code Guardian
Tu auditor de confianza con análisis estático e IA.
*   Generación de reportes de salud: *Security, Quality & Maintainability*.
*   Detección proactiva de hallazgos técnicos correlacionados con el dominio.

### 3. 🤖 Architecture Consultant
Agente de IA especializado en streaming y patrones complejos.
*   Consultas en tiempo real sobre **CQRS, DDD y Clean Architecture**.
*   **Zero-Latency UI**: Streaming de tokens vía **SignalR** para una experiencia fluida.

### 4. 📡 Trends Radar
Monitoreo y telemetría del ecosistema tecnológico.
*   Snapshots históricos para entender la tracción de frameworks y herramientas.
*   Análisis de "Traction Levels" (Emerging, Growing, Mainstream, Declining).

---

## 🛠️ Stack Tecnológico (The Powerhouse)

### Backend (.NET 9)
*   **Architecture:** Clean Architecture + Minimal APIs.
*   **Persistence:** PostgreSQL / pgvector + EF Core.
*   **Real-time:** SignalR Hubs con **Distributed Backplane**.
*   **Concurrency:** Redis para **Distributed Locking (Redlock)** y Rate Limiting.
*   **Patterns:** MediatR (CQRS) + Result Pattern para control de flujo.

### Frontend (Next.js 15)
*   **Core:** React 19 + App Router + Server Components (RSC).
*   **State:** TanStack Query (Server State) + Zustand (Client State).
*   **Resiliency:** `useOptimistic` para interacciones sin latencia percibida.
*   **Type Safety:** Contratos espejo mediante `openapi-typescript`.

---

## 🏗️ Filosofía de Diseño: "Purity over Convenience"

1.  **Domain Isolation:** El dominio tiene cero dependencias externas. La lógica vive pura y protegida.
2.  **Hybrid Data Flow:** Separamos carriles de comunicación:
    *   **Input (Command):** HTTP POST / Server Actions (Seguridad + Zod).
    *   **Output (Query/Event):** SignalR Streaming (Baja latencia).
3.  **Distributed Resilience:** Diseñado para escala horizontal. Redis orquesta colas de trabajos y sincronización de sockets entre réplicas.

---

## 📁 Estructura del Proyecto

```
src/
├── SentientArchitect.Domain/          # Entidades, interfaces y enums (Pure domain)
├── SentientArchitect.Application/     # Casos de uso y orquestación de agentes
├── SentientArchitect.Infrastructure/  # EF Core, Semantic Kernel, Integraciones
├── SentientArchitect.API/             # Endpoints, SignalR y Middleware
└── sentient-ui/                      # Next.js 15 Frontend (App Router)
```

---

## 🚀 Próximos Pasos
*   [x] Diseño Arquitectónico Core
*   [x] Implementación de Pilares Fundamentales
*   [x] Capa de Resiliencia Frontend (10/10)
*   [ ] Dockerización Completa (Development & Production)
*   [ ] Soporte para Multi-Tenant Architecture
