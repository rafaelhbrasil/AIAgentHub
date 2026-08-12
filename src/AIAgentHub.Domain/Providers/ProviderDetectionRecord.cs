namespace AIAgentHub.Domain.Providers;

public sealed class ProviderDetectionRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ProviderId { get; set; } = string.Empty;
    public ProviderStatus Status { get; set; }
    public string? Message { get; set; }
    public string? Version { get; set; }
    public string? ExecutablePath { get; set; }
    public bool IsInstalled { get; set; }
    public bool IsAuthenticated { get; set; }
    public DateTimeOffset? QuotaResetsAt { get; set; }
    public DateTimeOffset DetectedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
