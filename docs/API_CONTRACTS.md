# The Sentient Architect — API Contracts

## General Conventions

- **Style**: Minimal APIs (`.NET 9`)
- **Base path**: `/api/v1/`
- **Versioning**: URL path versioning (`/api/v1/`, `/api/v2/` when needed)
- **Auth**: JWT Bearer token on all endpoints except Auth group
- **Content-Type**: `application/json` for all request/response bodies
- **Pagination**: `?page=1&pageSize=20` with response wrapper `{ items: [], totalCount, page, pageSize }`
- **Errors**: Problem Details (RFC 7807) format: `{ type, title, status, detail, traceId }`
- **Dates**: ISO 8601 UTC (`2025-03-30T14:30:00Z`)
- **IDs**: Guid format

## Endpoint Groups

---

### 1. Auth (`/api/v1/auth`)

No JWT required on these endpoints.

#### POST `/api/v1/auth/register`
Create a new account. Default role: User.
```
Request:
{
  "email": "string",
  "displayName": "string",
  "password": "string"
}

Response 201:
{
  "userId": "guid",
  "email": "string",
  "displayName": "string",
  "role": "User"
}

Errors: 409 Conflict (email exists), 400 Bad Request (validation)
```

#### POST `/api/v1/auth/login`
```
Request:
{
  "email": "string",
  "password": "string"
}

Response 200:
{
  "accessToken": "string (JWT)",
  "refreshToken": "string",
  "expiresAt": "datetime",
  "user": {
    "id": "guid",
    "email": "string",
    "displayName": "string",
    "role": "string"
  }
}

Errors: 401 Unauthorized (bad credentials), 423 Locked (account locked)
```

#### POST `/api/v1/auth/refresh`
```
Request:
{
  "refreshToken": "string"
}

Response 200:
{
  "accessToken": "string (JWT)",
  "refreshToken": "string",
  "expiresAt": "datetime"
}

Errors: 401 Unauthorized (invalid/expired refresh token)
```

---

### 2. Knowledge (`/api/v1/knowledge`)

All endpoints require JWT. User sees personal + shared. Admin sees all.

#### POST `/api/v1/knowledge`
Ingest a new knowledge item (article, note, doc). Starts async processing.
```
Auth: User, Admin
Request:
{
  "title": "string",
  "content": "string",          // raw text or markdown
  "sourceUrl": "string?",       // nullable
  "type": "Article | Note | Documentation | Repository",
  "tags": ["string"]?           // optional manual tags
}

Response 201 Created:
{
  "id": "guid",
  "title": "string",
  "type": "Article | Note | Documentation | Repository",
  "status": "Pending | Processing | Completed | Failed",
  "chunksCreated": 3
}

Errors: 400 Bad Request (validation), 429 Too Many Requests (rate limit)
```

#### GET `/api/v1/knowledge`
List knowledge items with filtering and pagination.
```
Auth: User, Admin
Query params: ?page=1&pageSize=20&type=Article&tags=CQRS,DDD&search=keyword&status=Completed

Response 200:
{
  "items": [
    {
      "id": "guid",
      "title": "string",
      "type": "string",
      "summary": "string?",
      "sourceUrl": "string?",
      "tags": ["string"],
      "processingStatus": "string",
      "scope": "Personal | Shared",
      "createdAt": "datetime"
    }
  ],
  "totalCount": 42,
  "page": 1,
  "pageSize": 20
}
```

#### GET `/api/v1/knowledge/{id}`
Get full detail of a knowledge item.
```
Auth: User (own personal + shared), Admin (all)
Response 200:
{
  "id": "guid",
  "title": "string",
  "originalContent": "string",
  "summary": "string?",
  "sourceUrl": "string?",
  "type": "string",
  "tags": ["string"],
  "processingStatus": "string",
  "scope": "Personal | Shared",
  "repositoryInfo": { ... }?,       // if type == Repository
  "latestAnalysisReport": { ... }?, // if repo has been analyzed
  "createdAt": "datetime",
  "updatedAt": "datetime"
}

Errors: 404 Not Found, 403 Forbidden (not owner and not shared)
```

#### POST `/api/v1/knowledge/search`
Semantic search using RAG. This is the primary search endpoint.
```
Auth: User, Admin
Request:
{
  "query": "string",            // natural language question
  "maxResults": 10,             // default 5
  "types": ["Article", "Repository"]?,  // optional type filter
  "tags": ["string"]?,          // optional tag filter
  "includeShared": true         // default true
}

Response 200:
{
  "results": [
    {
      "knowledgeItemId": "guid",
      "title": "string",
      "matchedChunk": "string",     // the specific chunk that matched
      "similarityScore": 0.87,
      "type": "string",
      "tags": ["string"],
      "scope": "Personal | Shared"
    }
  ],
  "queryEmbeddingTimeMs": 45,
  "searchTimeMs": 120
}
```

#### DELETE `/api/v1/knowledge/{id}`
```
Auth: User (own personal only), Admin (any)
Response 204 No Content
Errors: 404, 403
```

#### POST `/api/v1/knowledge/{id}/publish`
Request publication of personal item to shared space.
```
Auth: User
Request:
{
  "reason": "string?"     // why this should be shared with the team
}

Response 202 Accepted:
{
  "publishRequestId": "guid",
  "status": "Pending"
}

Errors: 404, 403, 409 Conflict (already shared or pending request)
```

#### GET `/api/v1/tags`
List all existing tags. Essential for frontend autocomplete to prevent duplicates ("C#" vs "CSharp" vs "C-Sharp").
```
Auth: User, Admin
Query params: ?category=Technology&search=csh (prefix search for autocomplete)

Response 200:
{
  "items": [
    {
      "id": "guid",
      "name": "C#",
      "category": "Language",
      "isAutoGenerated": true,
      "usageCount": 15
    }
  ]
}
```

---

### 3. Repositories (`/api/v1/repositories`)

#### POST `/api/v1/repositories`
Submit a repository for analysis. Creates KnowledgeItem + RepositoryInfo + triggers Guardian.
```
Auth: User, Admin
Request:
{
  "gitUrl": "string",
  "trustLevel": "Internal | External",
  "tags": ["string"]?
}

Response 202 Accepted:
{
  "knowledgeItemId": "guid",
  "repositoryInfoId": "guid",
  "processingStatus": "Pending",
  "message": "Repository queued for cloning and analysis"
}

Errors: 400 (invalid URL), 409 (repo already exists), 429 (rate limit)
```

#### GET `/api/v1/repositories/{knowledgeItemId}/analysis`
Get all analysis reports for a repository (history).
```
Auth: User (own + shared), Admin (all)
Response 200:
{
  "repositoryInfo": {
    "gitUrl": "string",
    "primaryLanguage": "string",
    "trustLevel": "string",
    "stars": 42,
    "openIssues": 3,
    "lastCommitDate": "datetime"
  },
  "reports": [
    {
      "id": "guid",
      "analysisType": "Full",
      "overallHealthScore": 78.5,
      "securityScore": 85.0,
      "qualityScore": 72.0,
      "maintainabilityScore": 80.0,
      "findingsCount": { "critical": 0, "high": 2, "medium": 5, "low": 8 },
      "executedAt": "datetime",
      "analysisDurationSeconds": 45
    }
  ]
}
```

#### GET `/api/v1/repositories/{knowledgeItemId}/analysis/{reportId}/findings`
Get detailed findings for a specific analysis report.
```
Auth: User (own + shared), Admin (all)
Query params: ?severity=High,Critical&category=Security

Response 200:
{
  "items": [
    {
      "id": "guid",
      "severity": "High",
      "category": "Security",
      "title": "Vulnerable dependency: Newtonsoft.Json < 13.0",
      "description": "string",
      "filePath": "src/Services/Parser.cs",
      "recommendation": "Update to Newtonsoft.Json >= 13.0.3",
      "isResolved": false
    }
  ],
  "totalCount": 15
}
```

#### POST `/api/v1/repositories/{knowledgeItemId}/reanalyze`
Trigger a fresh analysis of an existing repository.
```
Auth: User (own), Admin (any)
Response 202 Accepted:
{
  "reportId": "guid",
  "message": "Re-analysis queued"
}
```

---

### 4. Conversations (`/api/v1/conversations`)

#### POST `/api/v1/conversations`
Start a new conversation.
```
Auth: User, Admin
Request:
{
  "title": "string?",                                     // optional
  "agentType": "Knowledge | Consultant | Radar",         // default: Knowledge
  "activeRepositoryId": "guid?"                          // only valid for Consultant
}

Response 201:
{
  "id": "guid",
  "title": "New conversation",
  "status": "Active",
  "createdAt": "datetime"
}
```

#### GET `/api/v1/conversations`
List user's conversations.
```
Auth: User, Admin
Query params: ?status=Active&page=1&pageSize=20

Response 200:
{
  "items": [
    {
      "id": "guid",
      "title": "string",
      "objective": "string?",
      "status": "Active | Completed | Archived",
      "lastMessageAt": "datetime",
      "messageCount": 12
    }
  ],
  "totalCount": 8,
  "page": 1,
  "pageSize": 20
}
```

#### GET `/api/v1/conversations/{id}`
Get conversation with recent messages (not full history — use ConversationSummary).
```
Auth: User (own), Admin (all)
Response 200:
{
  "id": "guid",
  "title": "string",
  "objective": "string?",
  "status": "string",
  "userProfile": { ... },                // profile snapshot used in this conversation
  "latestSummary": {                     // compressed history
    "summaryText": "string",
    "keyDecisions": ["string"],
    "openQuestions": ["string"]
  },
  "recentMessages": [                   // last N messages (within token budget)
    {
      "id": "guid",
      "role": "User | Assistant | System",
      "content": "string",
      "retrievedContextIds": ["guid"],
      "tokensUsed": 350,
      "createdAt": "datetime"
    }
  ],
  "createdAt": "datetime",
  "lastMessageAt": "datetime"
}
```

#### POST `/api/v1/conversations/{id}/messages`
Send a message. Response comes via ConversationHub (streaming), not this endpoint.
```
Auth: User (own), Admin (all)
Request:
{
  "content": "string"
}

Response 202 Accepted:
{
  "messageId": "guid",
  "status": "Processing",
  "message": "Response streaming via ConversationHub"
}

Errors: 404, 403, 429 (token quota exceeded)
```

#### PATCH `/api/v1/conversations/{id}`
Update conversation status (archive, complete).
```
Auth: User (own), Admin (all)
Request:
{
  "status": "Completed | Archived"
}

Response 200: { updated conversation }
```

#### GET `/api/v1/conversations/{id}/recommendations`
Get architecture recommendations generated in this conversation.
```
Auth: User (own), Admin (all)
Response 200:
{
  "items": [
    {
      "id": "guid",
      "problem": "string",
      "recommendedPatterns": ["CQRS", "Event Sourcing"],
      "proposedStack": ["RabbitMQ", "MassTransit"],
      "tradeOffs": "string",
      "confidence": "High | Medium | Low",
      "createdAt": "datetime"
    }
  ]
}
```

---

### 5. Profile (`/api/v1/profile`)

#### GET `/api/v1/profile`
Get current user's profile.
```
Auth: User, Admin
Response 200:
{
  "id": "guid",
  "preferredStack": ["C#", ".NET 9", "PostgreSQL"],
  "knownPatterns": ["CQRS", "Clean Architecture"],
  "infrastructureContext": "microservices on Azure",
  "teamSize": "small",
  "experienceLevel": "senior",
  "customNotes": "string",
  "lastUpdatedAt": "datetime"
}
```

#### PUT `/api/v1/profile`
Update profile manually.
```
Auth: User, Admin
Request:
{
  "preferredStack": ["string"],
  "knownPatterns": ["string"],
  "infrastructureContext": "string",
  "teamSize": "string",
  "experienceLevel": "string",
  "customNotes": "string"
}

Response 200: { updated profile }
```

#### GET `/api/v1/profile/suggestions`
Get pending profile update suggestions from the AI.
```
Auth: User, Admin
Response 200:
{
  "items": [
    {
      "id": "guid",
      "field": "preferredStack",
      "suggestedValue": "Rust",
      "reason": "Mentioned Rust in 4 recent conversations",
      "status": "Pending",
      "createdAt": "datetime"
    }
  ]
}
```

#### PATCH `/api/v1/profile/suggestions/{id}`
Accept or reject a profile suggestion.
```
Auth: User, Admin
Request:
{
  "action": "Accept | Reject"
}

Response 200: { updated suggestion with new status }
```

#### GET `/api/v1/profile/token-usage`
Get current token usage and quota.
```
Auth: User, Admin
Response 200:
{
  "today": {
    "tokensConsumed": 45000,
    "dailyQuota": 100000,
    "percentUsed": 45.0,
    "quotaAction": "DegradeModel"
  },
  "last7Days": [
    { "date": "2025-03-30", "tokensConsumed": 45000 },
    { "date": "2025-03-29", "tokensConsumed": 82000 }
  ]
}
```

---

### 6. Admin (`/api/v1/admin`)

All endpoints require Admin role.

#### GET `/api/v1/admin/publish-requests`
List pending content publication requests.
```
Auth: Admin
Query params: ?status=Pending&page=1&pageSize=20

Response 200:
{
  "items": [
    {
      "id": "guid",
      "knowledgeItem": {
        "id": "guid",
        "title": "string",
        "type": "string",
        "summary": "string?"
      },
      "requestedBy": {
        "id": "guid",
        "displayName": "string",
        "role": "string"
      },
      "requestReason": "string?",
      "status": "Pending",
      "createdAt": "datetime"
    }
  ],
  "totalCount": 5
}
```

#### PATCH `/api/v1/admin/publish-requests/{id}`
Approve or reject a publication request.
```
Auth: Admin
Request:
{
  "action": "Approve | Reject",
  "rejectionReason": "string?"    // required if Reject
}

Response 200:
{
  "id": "guid",
  "status": "Approved | Rejected",
  "reviewedAt": "datetime"
}
```

#### GET `/api/v1/admin/users`
List all users in the tenant.
```
Auth: Admin
Response 200:
{
  "items": [
    {
      "id": "guid",
      "email": "string",
      "displayName": "string",
      "role": "string",
      "createdAt": "datetime",
      "todayTokenUsage": 45000
    }
  ]
}
```

#### PATCH `/api/v1/admin/users/{id}/role`
Change a user's role.
```
Auth: Admin
Request:
{
  "role": "Admin | User"
}

Response 200: { updated user }
```

#### PATCH `/api/v1/admin/users/{id}/quota`
Override token quota for a specific user.
```
Auth: Admin
Request:
{
  "dailyQuota": 200000,
  "quotaAction": "Warn | DegradeModel | Block"
}

Response 200: { updated quota settings }
```

#### POST `/api/v1/admin/trends/sync`
Force an immediate Trends Radar scan (normally runs on daily timer).
```
Auth: Admin
Request:
{
  "sourcesFilter": ["string"]?   // optional: scan only specific sources. Null = all sources
}

Response 202 Accepted:
{
  "message": "Trend scan queued",
  "estimatedDurationMinutes": 5
}

Errors: 409 Conflict (scan already in progress)
```

---

### 7. Trends (`/api/v1/trends`)

#### GET `/api/v1/trends`
List detected technology trends.
```
Auth: User, Admin
Query params: ?category=Framework&traction=Growing&minRelevance=50&page=1&pageSize=20

Response 200:
{
  "items": [
    {
      "id": "guid",
      "name": "Aspire",
      "category": "Framework",
      "tractionLevel": "Growing",
      "relevanceScore": 85.0,
      "summary": "string",
      "sources": ["url1", "url2"],
      "firstDetectedAt": "datetime",
      "lastUpdatedAt": "datetime"
    }
  ],
  "totalCount": 23
}
```

#### GET `/api/v1/trends/{id}/snapshots`
Get historical trend evolution.
```
Auth: User, Admin
Response 200:
{
  "trend": { "id": "guid", "name": "Aspire" },
  "snapshots": [
    {
      "tractionLevel": "Emerging",
      "mentionCount": 12,
      "sentimentScore": 0.72,
      "snapshotDate": "2025-01-15"
    },
    {
      "tractionLevel": "Growing",
      "mentionCount": 45,
      "sentimentScore": 0.81,
      "snapshotDate": "2025-02-15"
    }
  ]
}
```

---

## SignalR Hubs

### ConversationHub (`/hubs/conversation`)

Real-time streaming of Consultant responses and conversation events.

**Connection**: Client connects with JWT token. Server validates and associates with userId.

**Server → Client events:**
```
ReceiveMessageChunk(string conversationId, string chunk)
  → Streaming text chunks as the LLM generates the response

MessageCompleted(string conversationId, string messageId, int tokensUsed)
  → Signals end of response with metadata

ConversationSummaryGenerated(string conversationId, string summaryPreview)
  → Notification that a summary was auto-generated

ProfileSuggestionDetected(string suggestionId, string field, string value, string reason)
  → Real-time notification of profile update suggestion

TokenQuotaWarning(long consumed, long quota, float percentUsed)
  → Warning when usage approaches daily limit

Error(string conversationId, string errorMessage)
  → Error during processing
```

**Client → Server methods:**
```
SendMessage(string conversationId, string content)
  → Alternative to REST POST for real-time feel (same processing pipeline)

StopGeneration(string conversationId)
  → Cancel current LLM generation (CancellationToken)
```

### AnalysisHub (`/hubs/analysis`)

Real-time progress of Code Guardian analysis.

**Connection**: Client connects with JWT token.

**Server → Client events:**
```
AnalysisStarted(string knowledgeItemId, string gitUrl)
  → Cloning has begun

AnalysisProgress(string knowledgeItemId, string phase, int percentComplete)
  → Progress updates. Phases: "Cloning", "Analyzing", "ScanningDependencies", "GeneratingReport"

AnalysisFindingDetected(string knowledgeItemId, string severity, string title)
  → Real-time finding notification (as they're discovered)

AnalysisCompleted(string knowledgeItemId, string reportId, decimal overallScore)
  → Analysis finished successfully

AnalysisFailed(string knowledgeItemId, string errorMessage)
  → Analysis failed (timeout, clone error, etc.)
```

### IngestionHub (`/hubs/ingestion`)

Real-time progress of content ingestion pipeline.

**Server → Client events:**
```
IngestionStarted(string knowledgeItemId, string title)
  → Processing has begun

IngestionProgress(string knowledgeItemId, string phase, int percentComplete)
  → Phases: "Extracting", "Summarizing", "Chunking", "Embedding", "Tagging"

IngestionCompleted(string knowledgeItemId, string title, int chunksCreated)
  → Content fully processed and searchable

IngestionFailed(string knowledgeItemId, string errorMessage)
  → Processing failed
```

### Hub-REST ID Consistency (Critical for UX)
All hubs use `knowledgeItemId` and `conversationId` as their primary correlation key — the SAME IDs returned by the REST `202 Accepted` responses. This enables the frontend pattern:
1. `POST /api/v1/knowledge` → receives `{ id: "abc-123" }` with 202
2. Client subscribes to IngestionHub events filtered by `knowledgeItemId == "abc-123"`
3. UI immediately shows a progress bar that updates in real-time
4. No polling, no page refresh needed

Same pattern applies to repositories (AnalysisHub) and conversations (ConversationHub).

---

## Response Status Codes

| Code | Usage |
|------|-------|
| 200 | Successful read/update |
| 201 | Resource created (with Location header) |
| 202 | Accepted for async processing (ingestion, analysis, messages) |
| 204 | Successful delete |
| 400 | Validation errors |
| 401 | Missing or invalid JWT |
| 403 | Insufficient role/permissions |
| 404 | Resource not found |
| 409 | Conflict (duplicate email, already published, etc.) |
| 423 | Account locked |
| 429 | Rate limit or token quota exceeded |
| 500 | Internal server error (with Problem Details) |