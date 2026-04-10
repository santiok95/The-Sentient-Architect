import { authHandlers } from './handlers/auth.handlers'
import { knowledgeHandlers } from './handlers/knowledge.handlers'
import { conversationHandlers } from './handlers/conversation.handlers'
import { repositoryHandlers } from './handlers/repository.handlers'

export const handlers = [...authHandlers, ...knowledgeHandlers, ...conversationHandlers, ...repositoryHandlers]
