using AIAgentHub.Domain.Configuration;
using AIAgentHub.Domain.Security;

namespace AgentHub.UnitTests.Domain.Security;

public sealed class ServerSettingsTests
{
    [Fact]
    public void ServerSettings_Properties()
    {
        var server = new ServerSettings
        {
            IsSetupCompleted = true,
            NetworkMode = NetworkMode.Lan,
            ListeningPortHttps = 8443,
            ListeningPortHttp = 8080,
            SelectedInterfaces = ["127.0.0.1"],
            Theme = "light"
        };

        Assert.True(server.IsSetupCompleted);
        Assert.Equal(NetworkMode.Lan, server.NetworkMode);
        Assert.Equal(8443, server.ListeningPortHttps);
        Assert.Equal(8080, server.ListeningPortHttp);
        _ = Assert.Single(server.SelectedInterfaces);
        Assert.Equal("light", server.Theme);
    }
}
