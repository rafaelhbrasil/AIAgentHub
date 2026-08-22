namespace AIAgentHub.Domain.Providers;

public sealed record ProviderHeader(string Id, string DisplayName);

public abstract record ProviderRefreshEvent(string Type);

public sealed record ProviderRefreshInitEvent(
    int TotalInstalled,
    IReadOnlyList<ProviderHeader> Providers) : ProviderRefreshEvent("init");

public sealed record ProviderRefreshProgressEvent(
    ProviderInfo Provider,
    int CompletedCount,
    int TotalInstalled,
    int Percentage) : ProviderRefreshEvent("provider_completed");

public sealed record ProviderRefreshCompletedEvent(
    IReadOnlyList<ProviderInfo> Providers) : ProviderRefreshEvent("completed");
