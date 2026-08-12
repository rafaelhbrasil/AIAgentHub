using AIAgentHub.Domain.Providers;

namespace AIAgentHub.Application.Providers;

public sealed record ProviderDetectionResult(
    ProviderStatus Status,
    string? Message,
    DateTimeOffset? QuotaResetsAt,
    TimeSpan? CacheDuration);

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
    string Id { get; }
    string DisplayName { get; }
    string Description { get; }
    ProviderCapability Capabilities { get; }
    string? InstallInstructions { get; }
    string? InstallCommand { get; }
    string? AuthCommand { get; }
    string? DocumentationUrl { get; }
    Task<ProviderInfo> DetectAsync(CancellationToken cancellationToken = default);
    Task<ProviderDetectionResult> DetectDetailedAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default);
    Task<string?> StartSessionAsync(Guid conversationId, string workspacePath, string? modelId, CancellationToken cancellationToken = default);
    Task ExecuteAsync(ProviderExecutionContext context);
    Task<string> LaunchAuthenticationAsync(CancellationToken cancellationToken = default);
    Task AbortAsync(Guid conversationId);
    Task EndSessionAsync(Guid conversationId, CancellationToken cancellationToken = default);
}

public interface IProviderManager
{
    IReadOnlyList<IProvider> GetAllProviders();
    IProvider GetProvider(string id);
    Task<IReadOnlyList<ProviderInfo>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProviderInfo>> RefreshAllAsync(CancellationToken cancellationToken = default);
    Task<ProviderInfo?> GetProviderInfoAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ModelInfo>> GetModelsAsync(string providerId, bool forceRefresh = false, CancellationToken cancellationToken = default);
    Task<ProviderDetectionResult> DetectProviderDetailedAsync(string providerId, bool forceRefresh = false, CancellationToken cancellationToken = default);
    Task UpdateModelSettingsAsync(string providerId, Dictionary<string, bool> modelStates, CancellationToken cancellationToken = default);
}
