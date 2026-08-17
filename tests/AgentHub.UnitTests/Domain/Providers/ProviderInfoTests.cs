using AIAgentHub.Domain.Configuration;
using AIAgentHub.Domain.Providers;

namespace AgentHub.UnitTests.Domain.Providers;

public sealed class ProviderInfoTests
{
    [Fact]
    public void ProviderModels_CliOptions_Properties()
    {
        var info = new ProviderInfo
        {
            Id = "p1",
            DisplayName = "P1",
            Description = "D1",
            IsInstalled = true,
            IsAuthenticated = true,
            Status = ProviderStatus.Ready,
            Message = "Msg",
            Version = "1.0",
            ExecutablePath = "/bin",
            Capabilities = ProviderCapability.Streaming,
            SupportedModels = [new() { Id = "m1", DisplayName = "M1", Description = "Desc", ContextWindow = 100, IsDefault = true, IsDisplayed = true }],
            InstallInstructions = "inst",
            InstallCommand = "cmd",
            AuthCommand = "auth",
            DocumentationUrl = "doc"
        };
        Assert.Equal("p1", info.Id);
        _ = Assert.Single(info.SupportedModels);

        var setting = new ProviderModelSetting { ProviderId = "p1", ModelId = "m1", IsDisplayed = false };
        Assert.Equal("p1", setting.ProviderId);
        Assert.False(setting.IsDisplayed);

        var record = new ProviderDetectionRecord
        {
            ProviderId = "p1",
            Status = ProviderStatus.Ready,
            StatusDetails = "details",
            Version = "1.0",
            ExecutablePath = "/bin",
            IsInstalled = true,
            IsAuthenticated = true,
            QuotaResetsAt = DateTimeOffset.UtcNow.AddHours(1)
        };
        Assert.Equal("p1", record.ProviderId);
        _ = Assert.NotNull(record.QuotaResetsAt);

        var options = new CliExecutionOptions { Headless = false, Shell = "Bash", HeadedAutoCloseDelaySeconds = 15 };
        Assert.False(options.Headless);
        Assert.Equal("Bash", options.Shell);
        Assert.Equal(15, options.HeadedAutoCloseDelaySeconds);
    }
}
