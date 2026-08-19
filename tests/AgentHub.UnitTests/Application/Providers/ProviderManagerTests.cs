using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Providers;
using AIAgentHub.Domain.Repositories;

using NSubstitute;

namespace AgentHub.UnitTests.Application.Providers;

public sealed class ProviderManagerTests
{
    [Fact]
    public async Task ProviderManager_Operations_ShouldWork()
    {
        var provider = Substitute.For<IProvider>();
        _ = provider.Id.Returns("testprov");
        _ = provider.DisplayName.Returns("Test Provider");
        _ = provider.DetectAsync(Arg.Any<CancellationToken>()).Returns(new ProviderInfo { Id = "testprov", DisplayName = "Test Provider", IsInstalled = true });
        _ = provider.DetectDetailedAsync(Arg.Any<CancellationToken>()).Returns(new ProviderDetectionResult(ProviderStatus.Ready, "Ready", null));
        _ = provider.GetModelsAsync(Arg.Any<CancellationToken>()).Returns(new List<ModelInfo> { new() { Id = "m1", DisplayName = "M1" } });

        var modelSettingRepo = Substitute.For<IProviderModelSettingRepository>();
        var detectionRecordRepo = Substitute.For<IProviderDetectionRecordRepository>();

        var manager = new ProviderManager(
            new[] { provider },
            () => modelSettingRepo,
            () => detectionRecordRepo);

        var providersList = manager.GetAllProviders();
        _ = Assert.Single(providersList);

        var all = await manager.GetAllAsync();
        _ = Assert.Single(all);

        var info = await manager.GetProviderInfoAsync("testprov");
        Assert.NotNull(info);

        var notFoundInfo = await manager.GetProviderInfoAsync("missing");
        Assert.Null(notFoundInfo);

        var models = await manager.GetModelsAsync("testprov");
        _ = Assert.Single(models);

        var status = await manager.DetectProviderDetailedAsync("testprov");
        Assert.Equal(ProviderStatus.Ready, status.Status);

        var refreshed = await manager.RefreshAllAsync();
        _ = Assert.Single(refreshed);

        Assert.Equal(provider, manager.GetProvider("testprov"));
        _ = Assert.Throws<KeyNotFoundException>(() => manager.GetProvider("missing"));

        await manager.UpdateModelSettingsAsync("testprov", new Dictionary<string, bool> { { "m1", true } });
        _ = await Assert.ThrowsAsync<KeyNotFoundException>(() => manager.UpdateModelSettingsAsync("missing", []));
    }

    [Fact]
    public async Task GetAllAsync_WhenCachedInDb_ShouldReturnCachedModelsWithoutCallingProvider()
    {
        var provider = Substitute.For<IProvider>();
        _ = provider.Id.Returns("testprov");
        _ = provider.DisplayName.Returns("Test Provider");

        var modelSettingRepo = Substitute.For<IProviderModelSettingRepository>();
        var detectionRecordRepo = Substitute.For<IProviderDetectionRecordRepository>();

        _ = detectionRecordRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<ProviderDetectionRecord>
        {
            new()
            {
                ProviderId = "testprov",
                Status = ProviderStatus.Ready,
                StatusDetails = "Operational",
                IsInstalled = true,
                IsAuthenticated = true,
                Version = "1.0.0"
            }
        });

        _ = modelSettingRepo.GetByProviderIdAsync("testprov", Arg.Any<CancellationToken>()).Returns(new List<ProviderModelSetting>
        {
            new()
            {
                ProviderId = "testprov",
                ModelId = "cached-model-1",
                DisplayName = "Cached Model 1",
                IsDefault = true,
                IsDisplayed = true
            }
        });

        var manager = new ProviderManager(
            new[] { provider },
            () => modelSettingRepo,
            () => detectionRecordRepo);

        var all = await manager.GetAllAsync();

        _ = Assert.Single(all);
        Assert.Equal("testprov", all[0].Id);
        Assert.Equal(ProviderStatus.Ready, all[0].Status);
        _ = Assert.Single(all[0].SupportedModels);
        Assert.Equal("cached-model-1", all[0].SupportedModels[0].Id);

        // Verify that provider.GetModelsAsync and provider.DetectAsync were NEVER called
        _ = await provider.DidNotReceive().GetModelsAsync(Arg.Any<CancellationToken>());
        _ = await provider.DidNotReceive().DetectAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetModelsAsync_WhenForceRefreshFalseAndCached_ShouldReturnCachedModelsWithoutCallingProvider()
    {
        var provider = Substitute.For<IProvider>();
        _ = provider.Id.Returns("testprov");

        var modelSettingRepo = Substitute.For<IProviderModelSettingRepository>();
        var detectionRecordRepo = Substitute.For<IProviderDetectionRecordRepository>();

        _ = modelSettingRepo.GetByProviderIdAsync("testprov", Arg.Any<CancellationToken>()).Returns(new List<ProviderModelSetting>
        {
            new()
            {
                ProviderId = "testprov",
                ModelId = "model-a",
                DisplayName = "Model A",
                IsDefault = true,
                IsDisplayed = true
            }
        });

        var manager = new ProviderManager(
            new[] { provider },
            () => modelSettingRepo,
            () => detectionRecordRepo);

        var models = await manager.GetModelsAsync("testprov", forceRefresh: false);

        _ = Assert.Single(models);
        Assert.Equal("model-a", models[0].Id);
        Assert.Equal("Model A", models[0].DisplayName);

        _ = await provider.DidNotReceive().GetModelsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetModelsAsync_WhenForceRefreshTrue_ShouldCallProviderAndReconcile()
    {
        var provider = Substitute.For<IProvider>();
        _ = provider.Id.Returns("testprov");
        _ = provider.GetModelsAsync(Arg.Any<CancellationToken>()).Returns(new List<ModelInfo>
        {
            new() { Id = "live-model-1", DisplayName = "Live Model 1" }
        });

        var modelSettingRepo = Substitute.For<IProviderModelSettingRepository>();
        var detectionRecordRepo = Substitute.For<IProviderDetectionRecordRepository>();

        _ = modelSettingRepo.GetByProviderIdAsync("testprov", Arg.Any<CancellationToken>()).Returns(new List<ProviderModelSetting>
        {
            new()
            {
                ProviderId = "testprov",
                ModelId = "live-model-1",
                DisplayName = "Live Model 1",
                IsDisplayed = true
            }
        });

        var manager = new ProviderManager(
            new[] { provider },
            () => modelSettingRepo,
            () => detectionRecordRepo);

        var models = await manager.GetModelsAsync("testprov", forceRefresh: true);

        _ = Assert.Single(models);
        Assert.Equal("live-model-1", models[0].Id);

        _ = await provider.Received(1).GetModelsAsync(Arg.Any<CancellationToken>());
        await modelSettingRepo.Received(1).ReconcileAsync("testprov", Arg.Any<IReadOnlyList<ModelInfo>>(), Arg.Any<CancellationToken>());
    }
}
