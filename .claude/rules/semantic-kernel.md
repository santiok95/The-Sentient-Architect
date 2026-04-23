# Semantic Kernel Patterns

## SDK
- Base package: `Microsoft.SemanticKernel` (latest stable 1.x)
- Agent package: `Microsoft.SemanticKernel.Agents.Core`
- Use `ChatCompletionAgent` for all conversational agents

## Kernel Setup
- One Kernel instance per agent (not shared)
- Register AI service via `AddOpenAIChatCompletion()` or `AddAzureOpenAIChatCompletion()`
- Register plugins via `Kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(plugin))`
- Plugins can be shared across agents (same plugin instance registered in multiple kernels)

## Plugins
- Every plugin is a plain C# class
- Mark methods with `[KernelFunction]` attribute
- Use `[Description("...")]` on methods AND parameters — the LLM uses these to decide when to call them
- Descriptions must be specific and actionable, not vague
- Plugin methods can be async (return `Task<T>`)
- Inject dependencies via constructor (plugins are created through DI)

### Plugin method pattern:
```csharp
public class SearchPlugin
{
    private readonly IVectorStore _vectorStore;
    private readonly IKnowledgeRepository _knowledgeRepo;

    public SearchPlugin(IVectorStore vectorStore, IKnowledgeRepository knowledgeRepo)
    {
        _vectorStore = vectorStore;
        _knowledgeRepo = knowledgeRepo;
    }

    [KernelFunction, Description("Search the knowledge base using natural language. Returns relevant articles, notes, and repo analyses.")]
    public async Task<string> SearchByMeaningAsync(
        [Description("The user's question or search query in natural language")] string query,
        [Description("Maximum number of results to return")] int maxResults = 5)
    {
        // Generate embedding, search pgvector, fetch metadata, format results
    }
}
```

## Agent Configuration
- Use `FunctionChoiceBehavior.Auto()` to let the LLM decide when to call plugins
- System prompt (Instructions) defines the agent's personality and rules
- Keep Instructions concise — long prompts degrade response quality
- Each agent's Instructions should explicitly state what plugins it has and when to use them

## Conversational Agents
- Knowledge Agent: `SearchPlugin` + `IngestPlugin`
- Consultant Agent: `ProfilePlugin` + `SummaryPlugin` + `SearchPlugin` + `RepositoryContextPlugin` + `TrendsPlugin`
- Radar Agent: `TrendsPlugin` + `SearchPlugin` (trend-first, brain as validator)

## Conversation Management
- Use `ChatHistory` to maintain message history within a session
- Implement ConversationSummary compaction when history exceeds token threshold
- Context assembly order for Consultant: UserProfile → ConversationSummary → recent messages → RAG results

## Response Streaming
- Use `agent.InvokeStreamingAsync()` for real-time response streaming
- Connect to SignalR hub for pushing chunks to the frontend
- Track `TokensUsed` from the response metadata for cost monitoring

## Error Handling
- Wrap all LLM calls in try/catch — API failures should not crash the application
- Implement retry with exponential backoff for transient LLM API errors
- Log token usage and latency for every LLM call (observability)
- If a plugin call fails, the agent should gracefully inform the user, not retry infinitely

## Model Configuration
- Store model IDs and API keys in `appsettings.json` (or environment variables for secrets)
- Support configurable model selection (e.g., GPT-4o for Consultant, GPT-4o-mini for embeddings)
- The embedding model MUST be consistent — changing it requires re-embedding all content