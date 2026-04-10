import { authHandlers } from './handlers/auth.handlers'
import { knowledgeHandlers } from './handlers/knowledge.handlers'
import { conversationHandlers } from './handlers/conversation.handlers'
import { repositoryHandlers } from './handlers/repository.handlers'
import { trendsHandlers } from './handlers/trends.handlers'
import { adminHandlers } from './handlers/admin.handlers'

export const handlers = [...authHandlers, ...knowledgeHandlers, ...conversationHandlers, ...repositoryHandlers, ...trendsHandlers, ...adminHandlers]
