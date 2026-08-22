using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Providers;
using AIAgentHub.Domain.Repositories;
using NSubstitute;
using Xunit;

namespace AgentHub.UnitTests.Application.Providers;

public sealed class ProviderManagerStreamingTests
{
    [Fact]
    public async Task StreamRefreshAllAsync_FiltersUninstalledAndStreamsProgress()
    {
        // Arrange
        var mockInstalled = Substitute.For<IProvider>();
        _ = mockInstalled.Id.Returns("installed1");
        _ = mockInstalled.DisplayName.Returns("Installed Provider");
        _ = mockInstalled.IsInstalledFastCheck().Returns(true);
        _ = mockInstalled.DetectAsync(Arg.Any<CancellationToken>())
            .Returns(new ProviderInfo { Id = "installed1", DisplayName = "Installed Provider", Status = ProviderStatus.Ready });
        _ = mockInstalled.DetectDetailedAsync(Arg.Any<CancellationToken>())
            .Returns(new ProviderDetectionResult(ProviderStatus.Ready, "Ready", null));
        _ = mockInstalled.GetModelsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ModelInfo>());

        var mockUninstalled = Substitute.For<IProvider>();
        _ = mockUninstalled.Id.Returns("uninstalled1");
        _ = mockUninstalled.DisplayName.Returns("Uninstalled Provider");
        _ = mockUninstalled.IsInstalledFastCheck().Returns(false);
        _ = mockUninstalled.DetectAsync(Arg.Any<CancellationToken>())
            .Returns(new ProviderInfo { Id = "uninstalled1", DisplayName = "Uninstalled Provider", Status = ProviderStatus.NotInstalled });

        var modelSettingRepo = Substitute.For<IProviderModelSettingRepository>();
        var detectionRecordRepo = Substitute.For<IProviderDetectionRecordRepository>();

        var manager = new ProviderManager(
            new[] { mockInstalled, mockUninstalled },
            () => modelSettingRepo,
            () => detectionRecordRepo);

        // Act
        var events = new List<ProviderRefreshEvent>();
        await foreach (var evt in manager.StreamRefreshAllAsync(CancellationToken.None))
        {
            events.Add(evt);
        }

        // Assert
        Assert.Contains(events, e => e is ProviderRefreshInitEvent init && init.TotalInstalled == 1);
        Assert.Contains(events, e => e is ProviderRefreshProgressEvent prog && prog.Provider.Id == "installed1" && prog.Percentage == 100);
        Assert.Contains(events, e => e is ProviderRefreshCompletedEvent);

        _ = mockUninstalled.DidNotReceive().DetectDetailedAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StreamRefreshAllAsync_WhenNoProvidersInstalled_YieldsInitAndCompletedImmediately()
    {
        // Arrange
        var mockUninstalled = Substitute.For<IProvider>();
        _ = mockUninstalled.Id.Returns("uninstalled1");
        _ = mockUninstalled.DisplayName.Returns("Uninstalled Provider");
        _ = mockUninstalled.IsInstalledFastCheck().Returns(false);
        _ = mockUninstalled.DetectAsync(Arg.Any<CancellationToken>())
            .Returns(new ProviderInfo { Id = "uninstalled1", DisplayName = "Uninstalled Provider", Status = ProviderStatus.NotInstalled });

        var modelSettingRepo = Substitute.For<IProviderModelSettingRepository>();
        var detectionRecordRepo = Substitute.For<IProviderDetectionRecordRepository>();

        var manager = new ProviderManager(
            new[] { mockUninstalled },
            () => modelSettingRepo,
            () => detectionRecordRepo);

        // Act
        var events = new List<ProviderRefreshEvent>();
        await foreach (var evt in manager.StreamRefreshAllAsync(CancellationToken.None))
        {
            events.Add(evt);
        }

        // Assert
        Assert.Equal(2, events.Count);
        Assert.IsType<ProviderRefreshInitEvent>(events[0]);
        Assert.Equal(0, ((ProviderRefreshInitEvent)events[0]).TotalInstalled);
        Assert.IsType<ProviderRefreshCompletedEvent>(events[1]);
    }
}
