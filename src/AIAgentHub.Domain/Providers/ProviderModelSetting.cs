namespace AIAgentHub.Domain.Providers;

public sealed class ProviderModelSetting
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ProviderId { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ContextWindow { get; set; }
    public bool IsDefault { get; set; }
    public bool IsDisplayed { get; set; } = true;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
