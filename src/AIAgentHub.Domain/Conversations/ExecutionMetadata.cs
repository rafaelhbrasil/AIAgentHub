namespace AIAgentHub.Domain.Conversations;

public enum MessageRole
{
    User = 0,
    Assistant = 1,
    System = 2,
    Tool = 3
}

public sealed class ExecutionMetadata
{
    public string? ProviderId { get; set; }
    public string? ModelId { get; set; }
    public string? ProviderSessionId { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string? Action { get; set; }
    public long DurationMs { get; set; }
    public int? Tokens { get; set; }
    public bool IsSuccess { get; set; } = true;
    public string? ErrorMessage { get; set; }
}
