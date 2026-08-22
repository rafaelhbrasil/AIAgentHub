using System.Text.Json.Serialization;

namespace AIAgentHub.Domain.Common;

public abstract class Entity
{
    [JsonInclude]
    public Guid Id { get; set; } = Guid.NewGuid();
}

public abstract class AggregateRoot : Entity
{
}
