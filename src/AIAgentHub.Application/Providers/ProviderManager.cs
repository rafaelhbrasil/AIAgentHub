using System.Collections.Concurrent;
using AIAgentHub.Domain.Providers;
using AIAgentHub.Domain.Repositories;

namespace AIAgentHub.Application.Providers;

public sealed class ProviderManager : IProviderManager
{
    private readonly IEnumerable<IProvider> _providers;
    private readonly Func<IProviderModelSettingRepository> _repositoryFactory;
    private readonly Func<IProviderDetectionRecordRepository> _detectionRecordFactory;
    private readonly ConcurrentDictionary<string, IReadOnlyList<ModelInfo>> _modelCache = new();

    public ProviderManager(
        IEnumerable<IProvider> providers, 
        Func<IProviderModelSettingRepository> repositoryFactory,
        Func<IProviderDetectionRecordRepository> detectionRecordFactory)
    {
        _providers = providers;
        _repositoryFactory = repositoryFactory;
        _detectionRecordFactory = detectionRecordFactory;
    }

    public IReadOnlyList<IProvider> GetAllProviders() => _providers.ToList();

    public IProvider GetProvider(string id)
    {
        var provider = _providers.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
        if (provider == null)
            throw new KeyNotFoundException($"AI Provider with ID '{id}' was not found.");
        return provider;
    }

    public async Task<IReadOnlyList<ProviderInfo>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // Read from DB cache first
        var dbRecords = await GetDetectionRecordsAsync(cancellationToken);
        var results = new List<ProviderInfo>();

        foreach (var provider in _providers)
        {
            var dbRecord = dbRecords.FirstOrDefault(r => r.ProviderId == provider.Id);
            
            ProviderInfo info;
            if (dbRecord != null)
            {
                // Use cached data from DB
                info = new ProviderInfo
                {
                    Id = provider.Id,
                    DisplayName = provider.DisplayName,
                    Description = provider.Description,
                    IsInstalled = dbRecord.IsInstalled,
                    IsAuthenticated = dbRecord.IsAuthenticated,
                    Status = dbRecord.Status,
                    Message = dbRecord.Message,
                    Version = dbRecord.Version,
                    ExecutablePath = dbRecord.ExecutablePath,
                    Capabilities = provider.Capabilities,
                    SupportedModels = new List<ModelInfo>(),
                    InstallInstructions = provider.InstallInstructions,
                    InstallCommand = provider.InstallCommand,
                    AuthCommand = provider.AuthCommand,
                    DocumentationUrl = provider.DocumentationUrl
                };
            }
            else
            {
                // No DB cache, run detection
                info = await provider.DetectAsync(cancellationToken);
                info.Message = info.IsInstalled ? "Provider is ready to use." : "Provider is not installed.";
                
                // Persist to DB
                await PersistDetectionResultAsync(provider.Id, info, cancellationToken);
            }

            // Get models (cached in memory or from provider)
            var models = await GetModelsAsync(provider.Id, false, cancellationToken);
            info.SupportedModels = models.ToList();
            
            results.Add(info);
        }

        return results;
    }

    public async Task<IReadOnlyList<ProviderInfo>> RefreshAllAsync(CancellationToken cancellationToken = default)
    {
        // Run detection for all providers in parallel
        var tasks = _providers.Select(async provider =>
        {
            var info = await provider.DetectAsync(cancellationToken);
            info.Message = info.IsInstalled ? "Provider is ready to use." : "Provider is not installed.";
            await PersistDetectionResultAsync(provider.Id, info, cancellationToken);
            
            // Also refresh models
            var models = await GetModelsAsync(provider.Id, forceRefresh: true, cancellationToken);
            info.SupportedModels = models.ToList();
            
            return info;
        });

        var results = await Task.WhenAll(tasks);
        return results.ToList();
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
        }

        // Check DB cache if not forcing refresh
        if (!forceRefresh && _detectionRecordFactory != null)
        {
            var recordRepo = _detectionRecordFactory();
            var dbRecord = await recordRepo.GetByProviderIdAsync(providerId, cancellationToken);
            if (dbRecord != null)
            {
                return new ProviderDetectionResult(
                    dbRecord.Status,
                    dbRecord.Message,
                    dbRecord.QuotaResetsAt,
                    GetCacheDurationForStatus(dbRecord.Status));
            }
        }

        var result = await provider.DetectDetailedAsync(cancellationToken);

        if (result.Status == ProviderStatus.Ready)
        {
            await GetModelsAsync(providerId, forceRefresh: true, cancellationToken);
        }

        // Persist detailed detection result to DB
        await PersistDetailedResultAsync(providerId, result, cancellationToken);

        return result;
    }

    private async Task<IReadOnlyList<ProviderDetectionRecord>> GetDetectionRecordsAsync(CancellationToken cancellationToken)
    {
        if (_detectionRecordFactory == null)
            return Array.Empty<ProviderDetectionRecord>();

        var repo = _detectionRecordFactory();
        return await repo.GetAllAsync(cancellationToken);
    }

    private async Task PersistDetectionResultAsync(string providerId, ProviderInfo info, CancellationToken cancellationToken)
    {
        if (_detectionRecordFactory == null) return;

        var repo = _detectionRecordFactory();
        var record = new ProviderDetectionRecord
        {
            ProviderId = providerId,
            Status = info.Status,
            Message = info.IsInstalled ? "Provider is ready to use." : "Provider is not installed.",
            Version = info.Version,
            ExecutablePath = info.ExecutablePath,
            IsInstalled = info.IsInstalled,
            IsAuthenticated = info.IsAuthenticated,
            DetectedAtUtc = DateTimeOffset.UtcNow
        };

        await repo.UpsertAsync(record, cancellationToken);
    }

    private async Task PersistDetailedResultAsync(string providerId, ProviderDetectionResult result, CancellationToken cancellationToken)
    {
        if (_detectionRecordFactory == null) return;

        var repo = _detectionRecordFactory();
        var existing = await repo.GetByProviderIdAsync(providerId, cancellationToken);

        var record = existing ?? new ProviderDetectionRecord { ProviderId = providerId };
        record.Status = result.Status;
        record.Message = result.Message;
        record.QuotaResetsAt = result.QuotaResetsAt;
        record.DetectedAtUtc = DateTimeOffset.UtcNow;

        await repo.UpsertAsync(record, cancellationToken);
    }

    private static TimeSpan? GetCacheDurationForStatus(ProviderStatus status) => status switch
    {
        ProviderStatus.Ready => TimeSpan.FromMinutes(5),
        ProviderStatus.Error => TimeSpan.FromMinutes(10),
        ProviderStatus.Unauthenticated => TimeSpan.FromMinutes(30),
        ProviderStatus.QuotaExceeded => TimeSpan.FromHours(1),
        ProviderStatus.NotInstalled => TimeSpan.FromHours(1),
        _ => TimeSpan.FromMinutes(5)
    };
}
