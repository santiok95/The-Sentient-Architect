# Testing Strategy

## Structure
- `tests/SentientArchitect.UnitTests/` → Domain logic, use cases, mappers
- `tests/SentientArchitect.IntegrationTests/` → EF Core, pgvector, external APIs

## Unit Tests
- Test domain entity behavior (validation, state transitions)
- Test use cases with mocked repository interfaces
- Test mappers (DTO ↔ Entity conversions)
- No real database, no real AI calls
- Use xUnit + FluentAssertions + NSubstitute

## Integration Tests
- Use Testcontainers for PostgreSQL + pgvector
- Test EF Core configurations and migrations
- Test vector similarity search with real pgvector
- Test Semantic Kernel pipeline with mocked LLM responses

## What NOT to Test
- Don't unit test EF Core configurations (that's integration test territory)
- Don't test LLM response quality (non-deterministic)
- Don't test external API responses (mock them)

## Conventions
- Test class name: `{ClassUnderTest}Tests`
- Test method name: `{Method}_Should{ExpectedBehavior}_When{Condition}`
- One assertion per test when possible
- Use builders/fixtures for complex entity construction
