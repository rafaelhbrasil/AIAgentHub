using System.Runtime.CompilerServices;
using System.Threading.Channels;
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
                var detectedInfo = await provider.DetectAsync(cancellationToken);
                info = detectedInfo ?? new ProviderInfo
                {
                    Id = provider.Id,
                    DisplayName = provider.DisplayName,
                    Description = provider.Description,
                    Status = ProviderStatus.NotInstalled,
                    Capabilities = provider.Capabilities,
                    InstallInstructions = provider.InstallInstructions,
                    InstallCommand = provider.InstallCommand,
                    AuthCommand = provider.AuthCommand,
                    DocumentationUrl = provider.DocumentationUrl
                };

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

    public async IAsyncEnumerable<ProviderRefreshEvent> StreamRefreshAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var installedProviders = new List<IProvider>();
        var uninstalledProviders = new List<IProvider>();

        foreach (var provider in _providers)
        {
            if (provider.IsInstalledFastCheck())
            {
                installedProviders.Add(provider);
            }
            else
            {
                uninstalledProviders.Add(provider);
            }
        }

        // Immediately record uninstalled providers in DB without running child processes
        foreach (var uninstalled in uninstalledProviders)
        {
            var notInstalledInfo = new ProviderInfo
            {
                Id = uninstalled.Id,
                DisplayName = uninstalled.DisplayName,
                Description = uninstalled.Description,
                IsInstalled = false,
                IsAuthenticated = false,
                Status = ProviderStatus.NotInstalled,
                Message = $"{uninstalled.DisplayName} is not installed.",
                Capabilities = uninstalled.Capabilities,
                SupportedModels = [],
                InstallInstructions = uninstalled.InstallInstructions,
                InstallCommand = uninstalled.InstallCommand,
                AuthCommand = uninstalled.AuthCommand,
                DocumentationUrl = uninstalled.DocumentationUrl
            };
            await PersistDetectionResultAsync(uninstalled.Id, notInstalledInfo, cancellationToken);
        }

        var totalInstalled = installedProviders.Count;
        var headers = installedProviders.Select(p => new ProviderHeader(p.Id, p.DisplayName)).ToList();

        yield return new ProviderRefreshInitEvent(totalInstalled, headers);

        if (totalInstalled == 0)
        {
            var allCached = await GetAllAsync(cancellationToken);
            yield return new ProviderRefreshCompletedEvent(allCached);
            yield break;
        }

        var completedChannel = Channel.CreateUnbounded<(ProviderInfo Info, int CompletedCount)>();
        var completedCounter = 0;

        var tasks = installedProviders.Select(async provider =>
        {
            ProviderInfo info;
            try
            {
                var detailed = await provider.DetectDetailedAsync(cancellationToken);
                await PersistDetailedResultAsync(provider.Id, detailed, cancellationToken);

                var models = detailed.Status == ProviderStatus.Ready
                    ? await GetModelsAsync(provider.Id, forceRefresh: true, cancellationToken)
                    : await GetModelsAsync(provider.Id, forceRefresh: false, cancellationToken);

                info = new ProviderInfo
                {
                    Id = provider.Id,
                    DisplayName = provider.DisplayName,
                    Description = provider.Description,
                    IsInstalled = detailed.Status != ProviderStatus.NotInstalled,
                    IsAuthenticated = detailed.Status == ProviderStatus.Ready,
                    Status = detailed.Status,
                    Message = detailed.Message,
                    QuotaResetsAt = detailed.QuotaResetsAt,
                    Capabilities = provider.Capabilities,
                    SupportedModels = [.. models],
                    InstallInstructions = provider.InstallInstructions,
                    InstallCommand = provider.InstallCommand,
                    AuthCommand = provider.AuthCommand,
                    DocumentationUrl = provider.DocumentationUrl
                };
                await PersistDetectionResultAsync(provider.Id, info, cancellationToken);
            }
            catch (Exception ex)
            {
                info = new ProviderInfo
                {
                    Id = provider.Id,
                    DisplayName = provider.DisplayName,
                    Description = provider.Description,
                    IsInstalled = true,
                    IsAuthenticated = false,
                    Status = ProviderStatus.Error,
                    Message = $"Detection failed: {ex.Message}",
                    Capabilities = provider.Capabilities,
                    SupportedModels = [],
                    InstallInstructions = provider.InstallInstructions,
                    InstallCommand = provider.InstallCommand,
                    AuthCommand = provider.AuthCommand,
                    DocumentationUrl = provider.DocumentationUrl
                };
                await PersistDetectionResultAsync(provider.Id, info, cancellationToken);
            }

            var count = Interlocked.Increment(ref completedCounter);
            await completedChannel.Writer.WriteAsync((info, count), cancellationToken);
        });

        _ = Task.WhenAll(tasks).ContinueWith(_ => completedChannel.Writer.Complete(), cancellationToken);

        while (await completedChannel.Reader.WaitToReadAsync(cancellationToken))
        {
            while (completedChannel.Reader.TryRead(out var item))
            {
                var percentage = (int)Math.Round((double)item.CompletedCount / totalInstalled * 100.0);
                yield return new ProviderRefreshProgressEvent(item.Info, item.CompletedCount, totalInstalled, percentage);
            }
        }

        var allFinal = await GetAllAsync(cancellationToken);
        yield return new ProviderRefreshCompletedEvent(allFinal);
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

        if (!forceRefresh && _repositoryFactory != null)
        {
            var repo = _repositoryFactory();
            var dbModels = await repo.GetByProviderIdAsync(providerId, cancellationToken);
            if (dbModels.Count > 0)
            {
                return dbModels.Select(m => new ModelInfo
                {
                    Id = m.ModelId,
                    DisplayName = m.DisplayName,
                    Description = m.Description,
                    ContextWindow = m.ContextWindow,
                    IsDefault = m.IsDefault,
                    IsDisplayed = m.IsDisplayed
                }).ToList();
            }

            if (_detectionRecordFactory != null)
            {
                var detectionRepo = _detectionRecordFactory();
                var dbRecord = await detectionRepo.GetByProviderIdAsync(providerId, cancellationToken);
                if (dbRecord != null)
                {
                    // Detection was already performed and cached; return default delegation model from memory without spawning CLI subprocess
                    return
                    [
                        new()
                        {
                            Id = "default",
                            DisplayName = "Default",
                            Description = "Default model. The model will not be enforced, and whatever was set or used last directly in the provider CLI will remain active without being overridden.",
                            ContextWindow = null,
                            IsDefault = true,
                            IsDisplayed = true
                        }
                    ];
                }
            }
        }

        var rawModels = await provider.GetModelsAsync(cancellationToken);

        if (_repositoryFactory != null)
        {
            var repo = _repositoryFactory();
            await repo.ReconcileAsync(providerId, rawModels, cancellationToken);
            var dbModels = await repo.GetByProviderIdAsync(providerId, cancellationToken);
            if (dbModels.Count > 0)
            {
                var dbMap = dbModels.ToDictionary(m => m.ModelId, m => m, StringComparer.OrdinalIgnoreCase);
                foreach (var rm in rawModels)
                {
                    if (dbMap.TryGetValue(rm.Id, out var setting))
                    {
                        rm.IsDisplayed = setting.IsDisplayed;
                    }
                }
            }
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
