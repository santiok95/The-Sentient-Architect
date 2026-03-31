namespace SentientArchitect.Domain.Abstractions;

public abstract class BaseEntity : IEntity
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
}
