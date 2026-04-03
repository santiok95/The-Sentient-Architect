namespace SentientArchitect.Domain.Abstractions;

public abstract class BaseEntity : IEntity
{
    public Guid Id { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
}
