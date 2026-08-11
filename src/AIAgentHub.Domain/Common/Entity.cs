namespace AIAgentHub.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
}

public abstract class AggregateRoot : Entity
{
}
