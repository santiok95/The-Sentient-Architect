/**
 * MSW Node server for Vitest — intercepts all fetch calls in unit/integration tests.
 * Import this from vitest.setup.ts; never instantiate it inside components.
 */
import { setupServer } from 'msw/node'
import { handlers } from './handlers'

export const server = setupServer(...handlers)
