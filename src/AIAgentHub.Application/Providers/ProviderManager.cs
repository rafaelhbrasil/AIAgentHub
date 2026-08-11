using System.Collections.Concurrent;
using AIAgentHub.Domain.Providers;
using AIAgentHub.Domain.Repositories;

namespace AIAgentHub.Application.Providers;

public sealed class ProviderManager : IProviderManager
{
    private readonly IEnumerable<IProvider> _providers;
    private readonly Func<IProviderModelSettingRepository>? _repositoryFactory;
    private readonly ConcurrentDictionary<string, CachedDetectionResult> _detectionCache = new();
    private readonly ConcurrentDictionary<string, IReadOnlyList<ModelInfo>> _modelCache = new();

    private record CachedDetectionResult(ProviderDetectionResult Result, DateTimeOffset CachedAt);

    public ProviderManager(IEnumerable<IProvider> providers, Func<IProviderModelSettingRepository>? repositoryFactory = null)
    {
        _providers = providers;
        _repositoryFactory = repositoryFactory;
    }

    public IReadOnlyList<IProvider> GetAllProviders() => _providers.ToList();

    public IProvider GetProvider(string id)
    {
        var provider = _providers.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
        if (provider == null)
            throw new KeyNotFoundException($"AI Provider with ID '{id}' was not found.");
        return provider;
    }

    public async Task<IReadOnlyList<ProviderInfo>> DetectAllAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<ProviderInfo>();
        foreach (var provider in _providers)
        {
            var info = await provider.DetectAsync(cancellationToken);
            var models = await GetModelsAsync(provider.Id, false, cancellationToken);
            info.SupportedModels = models.ToList();
            results.Add(info);
        }
        return results;
    }

    public async Task<ProviderInfo?> GetProviderInfoAsync(string id, CancellationToken cancellationToken = default)
    {
        var provider = _providers.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
        if (provider == null) return null;
        var info = await provider.DetectAsync(cancellationToken);
        var models = await GetModelsAsync(id, false, cancellationToken);
        info.SupportedModels = models.ToList();
        return info;
    }

    public async Task<IReadOnlyList<ModelInfo>> GetModelsAsync(string providerId, bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        var provider = GetProvider(providerId);
        if (!forceRefresh && _modelCache.TryGetValue(providerId, out var cachedModels))
        {
            return cachedModels;
        }

        var rawModels = await provider.GetModelsAsync(cancellationToken);

        if (_repositoryFactory != null)
        {
            var repo = _repositoryFactory();
            await repo.ReconcileAsync(providerId, rawModels, cancellationToken);
        }

        _modelCache[providerId] = rawModels;
        return rawModels;
    }

    public async Task UpdateModelSettingsAsync(string providerId, Dictionary<string, bool> modelStates, CancellationToken cancellationToken = default)
    {
        GetProvider(providerId);

        if (_repositoryFactory != null)
        {
            var repo = _repositoryFactory();
            await repo.UpdateSettingsAsync(providerId, modelStates, cancellationToken);
        }

        _modelCache.TryRemove(providerId, out _);
    }

    public async Task<ProviderDetectionResult> DetectProviderDetailedAsync(string providerId, bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        var provider = GetProvider(providerId);

        if (forceRefresh)
        {
            _modelCache.TryRemove(providerId, out _);
            _detectionCache.TryRemove(providerId, out _);
        }

        if (!forceRefresh && _detectionCache.TryGetValue(providerId, out var cached))
        {
            if (cached.Result.CacheDuration.HasValue && DateTimeOffset.UtcNow < cached.CachedAt.Add(cached.Result.CacheDuration.Value))
            {
                return cached.Result;
            }
        }

        var result = await provider.DetectDetailedAsync(cancellationToken);

        if (result.Status == ProviderStatus.Ready)
        {
            await GetModelsAsync(providerId, forceRefresh: true, cancellationToken);
        }

        _detectionCache[providerId] = new CachedDetectionResult(result, DateTimeOffset.UtcNow);

        return result;
    }
}
