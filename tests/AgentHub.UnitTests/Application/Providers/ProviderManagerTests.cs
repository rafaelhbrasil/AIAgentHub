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
}
