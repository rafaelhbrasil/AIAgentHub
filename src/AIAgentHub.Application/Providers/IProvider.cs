using AIAgentHub.Domain.Providers;

namespace AIAgentHub.Application.Providers;

public sealed record ProviderDetectionResult(
    ProviderStatus Status,
    string? Message,
    DateTimeOffset? QuotaResetsAt);

public sealed record ProviderExecutionContext(
    Guid ConversationId,
    Guid WorkspaceId,
    string WorkspacePath,
    string Prompt,
    string? ModelId,
    string? ProviderSessionId,
    IReadOnlyList<string> IgnoredFiles,
    Func<string, Task> OnStreamToken,
    Func<string, string, Task<bool>> RequestPermission,
    CancellationToken CancellationToken,
    Func<string, Task>? OnSessionCreated = null,
    string? Effort = null);

public interface IProvider
{
    public string Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public ProviderCapability Capabilities { get; }
    public string? InstallInstructions { get; }
    public string? InstallCommand { get; }
    public string? AuthCommand { get; }
    public string? DocumentationUrl { get; }
    public Task<ProviderInfo> DetectAsync(CancellationToken cancellationToken = default);
    public bool IsInstalledFastCheck();
    public Task<ProviderDetectionResult> DetectDetailedAsync(CancellationToken cancellationToken = default);
    public Task<IReadOnlyList<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default);
    public Task<string?> StartSessionAsync(Guid conversationId, string workspacePath, string? modelId, CancellationToken cancellationToken = default);
    public Task ExecuteAsync(ProviderExecutionContext context);
    public Task<string> LaunchAuthenticationAsync(CancellationToken cancellationToken = default);
    public Task AbortAsync(Guid conversationId);
    public Task EndSessionAsync(Guid conversationId, CancellationToken cancellationToken = default);
}

public interface IProviderManager
{
    public IReadOnlyList<IProvider> GetAllProviders();
    public IProvider GetProvider(string id);
    public Task<IReadOnlyList<ProviderInfo>> GetAllAsync(CancellationToken cancellationToken = default);
    public Task<IReadOnlyList<ProviderInfo>> RefreshAllAsync(CancellationToken cancellationToken = default);
    public IAsyncEnumerable<ProviderRefreshEvent> StreamRefreshAllAsync(CancellationToken cancellationToken = default);
    public Task<ProviderInfo?> GetProviderInfoAsync(string id, CancellationToken cancellationToken = default);
    public Task<IReadOnlyList<ModelInfo>> GetModelsAsync(string providerId, bool forceRefresh = false, CancellationToken cancellationToken = default);
    public Task<ProviderDetectionResult> DetectProviderDetailedAsync(string providerId, bool forceRefresh = false, CancellationToken cancellationToken = default);
    public Task UpdateModelSettingsAsync(string providerId, Dictionary<string, bool> modelStates, CancellationToken cancellationToken = default);
}
