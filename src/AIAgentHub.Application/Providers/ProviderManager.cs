using System.Collections.Concurrent;

using AIAgentHub.Domain.Providers;
using AIAgentHub.Domain.Repositories;

namespace AIAgentHub.Application.Providers;

public sealed class ProviderManager(
    IEnumerable<IProvider> providers,
    Func<IProviderModelSettingRepository> repositoryFactory,
    Func<IProviderDetectionRecordRepository> detectionRecordFactory) : IProviderManager
{
    private readonly IEnumerable<IProvider> _providers = providers;
    private readonly Func<IProviderModelSettingRepository> _repositoryFactory = repositoryFactory;
    private readonly Func<IProviderDetectionRecordRepository> _detectionRecordFactory = detectionRecordFactory;
    private readonly ConcurrentDictionary<string, IReadOnlyList<ModelInfo>> _modelCache = new();

    public IReadOnlyList<IProvider> GetAllProviders() => _providers.ToList();

    public IProvider GetProvider(string id)
    {
        var provider = _providers.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)) ?? throw new KeyNotFoundException($"AI Provider with ID '{id}' was not found.");
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
                    Message = dbRecord.StatusDetails,
                    Version = dbRecord.Version,
                    ExecutablePath = dbRecord.ExecutablePath,
                    Capabilities = provider.Capabilities,
                    SupportedModels = [],
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
                if (string.IsNullOrEmpty(info.Message))
                {
                    info.Message = info.Status == ProviderStatus.Ready ? "Provider is operational and ready to use." : "Provider is not installed.";
                }

                // Persist to DB
                await PersistDetectionResultAsync(provider.Id, info, cancellationToken);
            }

            // Get models (cached in memory or from provider)
            var models = await GetModelsAsync(provider.Id, false, cancellationToken);
            info.SupportedModels = [.. models];

            results.Add(info);
        }

        return SortProviders(results);
    }

    public async Task<IReadOnlyList<ProviderInfo>> RefreshAllAsync(CancellationToken cancellationToken = default)
    {
        // Run detection for all providers in parallel
        var tasks = _providers.Select(async provider =>
        {
            var info = await provider.DetectAsync(cancellationToken);
            if (string.IsNullOrEmpty(info.Message))
            {
                info.Message = info.Status == ProviderStatus.Ready ? "Provider is operational and ready to use." : "Provider is not installed.";
            }
            await PersistDetectionResultAsync(provider.Id, info, cancellationToken);

            // Also refresh models
            var models = await GetModelsAsync(provider.Id, forceRefresh: true, cancellationToken);
            info.SupportedModels = [.. models];

            return info;
        });

        var results = await Task.WhenAll(tasks);
        return SortProviders([.. results]);
    }

    private static List<ProviderInfo> SortProviders(List<ProviderInfo> list) => [.. list.OrderBy(GetSortPriority).ThenBy(p => p.DisplayName)];

    private static int GetSortPriority(ProviderInfo p)
    {
        if (p.Id == "gemini" || (p.Message != null && p.Message.Contains("Discontinued", StringComparison.OrdinalIgnoreCase)))
        {
            return 99;
        }

        return p.Status == ProviderStatus.Ready
            ? 1
            : p.Status == ProviderStatus.Unauthenticated ? 2 : p.Status == ProviderStatus.NotInstalled ? 3 : 4;
    }

    public async Task<ProviderInfo?> GetProviderInfoAsync(string id, CancellationToken cancellationToken = default)
    {
        var provider = _providers.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
        if (provider == null)
        {
            return null;
        }

        var info = await provider.DetectAsync(cancellationToken);
        var models = await GetModelsAsync(id, false, cancellationToken);
        info.SupportedModels = [.. models];
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
        _ = GetProvider(providerId);

        if (_repositoryFactory != null)
        {
            var repo = _repositoryFactory();
            await repo.UpdateSettingsAsync(providerId, modelStates, cancellationToken);
        }

        _ = _modelCache.TryRemove(providerId, out _);
    }

    public async Task<ProviderDetectionResult> DetectProviderDetailedAsync(string providerId, bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        var provider = GetProvider(providerId);

        if (forceRefresh)
        {
            _ = _modelCache.TryRemove(providerId, out _);
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
                    dbRecord.StatusDetails,
                    dbRecord.QuotaResetsAt);
            }
        }

        var result = await provider.DetectDetailedAsync(cancellationToken);

        if (result.Status == ProviderStatus.Ready)
        {
            _ = await GetModelsAsync(providerId, forceRefresh: true, cancellationToken);
        }

        // Persist detailed detection result to DB
        await PersistDetailedResultAsync(providerId, result, cancellationToken);

        return result;
    }

    private async Task<IReadOnlyList<ProviderDetectionRecord>> GetDetectionRecordsAsync(CancellationToken cancellationToken)
    {
        if (_detectionRecordFactory == null)
        {
            return Array.Empty<ProviderDetectionRecord>();
        }

        var repo = _detectionRecordFactory();
        return await repo.GetAllAsync(cancellationToken);
    }

    private async Task PersistDetectionResultAsync(string providerId, ProviderInfo info, CancellationToken cancellationToken)
    {
        if (_detectionRecordFactory == null)
        {
            return;
        }

        var repo = _detectionRecordFactory();
        var record = new ProviderDetectionRecord
        {
            ProviderId = providerId,
            Status = info.Status,
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
        if (_detectionRecordFactory == null)
        {
            return;
        }

        var repo = _detectionRecordFactory();
        var existing = await repo.GetByProviderIdAsync(providerId, cancellationToken);

        var record = existing ?? new ProviderDetectionRecord { ProviderId = providerId };
        record.Status = result.Status;
        record.QuotaResetsAt = result.QuotaResetsAt;
        record.DetectedAtUtc = DateTimeOffset.UtcNow;

        await repo.UpsertAsync(record, cancellationToken);
    }
}
