namespace SentientArchitect.Application.Features.Conversations.Chat;

public sealed class ConversationOptions
{
    public const string SectionName = "Conversation";

    /// <summary>
    /// Number of messages in a conversation before compaction is triggered.
    /// Default: 20.
    /// </summary>
    public int CompactionThreshold { get; init; } = 20;
}
