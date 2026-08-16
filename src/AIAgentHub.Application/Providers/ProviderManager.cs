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

                // Read cached models from DB without invoking provider CLI
                var models = await GetModelsAsync(provider.Id, forceRefresh: false, cancellationToken);
                info.SupportedModels = [.. models];
            }
            else
            {
                // No DB cache yet (initial boot), run initial detection and seed DB
                info = await provider.DetectAsync(cancellationToken);
                if (string.IsNullOrEmpty(info.Message))
                {
                    info.Message = info.Status == ProviderStatus.Ready ? "Provider is operational and ready to use." : "Provider is not installed.";
                }

                // Persist detection result to DB
                await PersistDetectionResultAsync(provider.Id, info, cancellationToken);

                // Seed models to DB
                var models = await GetModelsAsync(provider.Id, forceRefresh: true, cancellationToken);
                info.SupportedModels = [.. models];
            }

            results.Add(info);
        }

        return SortProviders(results);
    }

    public async Task<IReadOnlyList<ProviderInfo>> RefreshAllAsync(CancellationToken cancellationToken = default)
    {
        // Run detection for all providers in parallel only when explicitly requested
        var tasks = _providers.Select(async provider =>
        {
            var info = await provider.DetectAsync(cancellationToken);
            if (string.IsNullOrEmpty(info.Message))
            {
                info.Message = info.Status == ProviderStatus.Ready ? "Provider is operational and ready to use." : "Provider is not installed.";
            }
            await PersistDetectionResultAsync(provider.Id, info, cancellationToken);

            // Refresh models from provider CLI and reconcile DB
            var models = await GetModelsAsync(provider.Id, forceRefresh: true, cancellationToken);
            info.SupportedModels = [.. models];

            return info;
        });

        var results = await Task.WhenAll(tasks);
        return SortProviders([.. results]);
    }

    private static List<ProviderInfo> SortProviders(List<ProviderInfo> list) => [.. list.OrderBy(GetProviderSortPriority).ThenBy(p => p.DisplayName)];

    private static int GetProviderSortPriority(ProviderInfo p)
    {
        return p.Status switch
        {
            ProviderStatus.Ready => 1,
            ProviderStatus.Unauthenticated => 2,
            ProviderStatus.NotInstalled => 3,
            ProviderStatus.QuotaExceeded => 4,
            ProviderStatus.Discontinued => 99,
            _ => 5
        };
    }

    public async Task<ProviderInfo?> GetProviderInfoAsync(string id, CancellationToken cancellationToken = default)
    {
        var provider = _providers.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
        if (provider == null)
        {
            return null;
        }

        if (_detectionRecordFactory != null)
        {
            var recordRepo = _detectionRecordFactory();
            var dbRecord = await recordRepo.GetByProviderIdAsync(id, cancellationToken);
            if (dbRecord != null)
            {
                var cachedModels = await GetModelsAsync(id, forceRefresh: false, cancellationToken);
                return new ProviderInfo
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
                    SupportedModels = [.. cachedModels],
                    InstallInstructions = provider.InstallInstructions,
                    InstallCommand = provider.InstallCommand,
                    AuthCommand = provider.AuthCommand,
                    DocumentationUrl = provider.DocumentationUrl
                };
            }
        }

        var info = await provider.DetectAsync(cancellationToken);
        await PersistDetectionResultAsync(id, info, cancellationToken);
        var models = await GetModelsAsync(id, forceRefresh: true, cancellationToken);
        info.SupportedModels = [.. models];
        return info;
    }

    public async Task<IReadOnlyList<ModelInfo>> GetModelsAsync(string providerId, bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        var provider = GetProvider(providerId);

        // If not forcing refresh, load cached models strictly from DB
        if (!forceRefresh && _repositoryFactory != null)
        {
            var repo = _repositoryFactory();
            var dbModels = await repo.GetByProviderIdAsync(providerId, cancellationToken);
            if (dbModels.Count > 0)
            {
                return dbModels.Select(m => new ModelInfo
                {
                    Id = m.ModelId,
                    DisplayName = !string.IsNullOrWhiteSpace(m.DisplayName) ? m.DisplayName : m.ModelId,
                    Description = m.Description,
                    ContextWindow = m.ContextWindow,
                    IsDefault = m.IsDefault,
                    IsDisplayed = m.IsDisplayed
                }).ToList();
            }
        }

        // Fetch fresh from provider CLI when explicitly refreshing or no DB records exist
        var rawModels = await provider.GetModelsAsync(cancellationToken);

        if (_repositoryFactory != null)
        {
            var repo = _repositoryFactory();
            await repo.ReconcileAsync(providerId, rawModels, cancellationToken);
        }

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
    }

    public async Task<ProviderDetectionResult> DetectProviderDetailedAsync(string providerId, bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        var provider = GetProvider(providerId);

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

        // Query provider CLI directly when refresh is requested
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
            StatusDetails = info.Message,
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
        record.StatusDetails = result.Message;
        record.QuotaResetsAt = result.QuotaResetsAt;
        record.DetectedAtUtc = DateTimeOffset.UtcNow;

        await repo.UpsertAsync(record, cancellationToken);
    }
}
