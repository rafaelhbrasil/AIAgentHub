using AIAgentHub.Domain.Common;

namespace AIAgentHub.Domain.Skills;

public sealed class Skill : Entity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Author { get; set; }
    public string? ProviderId { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string? FilePath { get; set; }
    public string? Content { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
