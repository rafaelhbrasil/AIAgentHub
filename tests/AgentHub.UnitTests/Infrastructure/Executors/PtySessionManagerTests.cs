using AIAgentHub.Infrastructure.Executors;
using AIAgentHub.Infrastructure.Realtime;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace AgentHub.UnitTests.Infrastructure.Executors;

public sealed class PtySessionManagerTests
{
    [Fact]
    public async Task GetOrCreateSessionAsync_StartsSession_And_TerminateSessionAsync_CleansUp()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var hubContext = Substitute.For<IHubContext<AgentHubHub>>();
        var hubClients = Substitute.For<IHubClients>();
        var clientProxy = Substitute.For<IClientProxy>();
        _ = hubClients.Group(Arg.Any<string>()).Returns(clientProxy);
        _ = hubContext.Clients.Returns(hubClients);

        var manager = new PtySessionManager(hubContext, NullLogger<PtySessionManager>.Instance);
        var conversationId = Guid.NewGuid();
        var workspacePath = Directory.GetCurrentDirectory();

        try
        {
            var sessionInfo = await manager.GetOrCreateSessionAsync(conversationId, workspacePath);

            Assert.NotNull(sessionInfo);
            Assert.Equal(conversationId, sessionInfo.ConversationId);
            Assert.True(sessionInfo.IsActive);
            Assert.True(manager.HasActiveSession(conversationId));

            // Verify reusing session returns the same conversation
            var secondCall = await manager.GetOrCreateSessionAsync(conversationId, workspacePath);
            Assert.Equal(sessionInfo.ConversationId, secondCall.ConversationId);

            // Send input to running session
            await manager.SendInputAsync(conversationId, "Write-Output 'PtyTest'\r\n");

            // Allow brief processing
            await Task.Delay(200);

            var recent = manager.GetRecentOutput(conversationId);
            Assert.NotNull(recent);
        }
        finally
        {
            await manager.TerminateSessionAsync(conversationId);
            Assert.False(manager.HasActiveSession(conversationId));
            await manager.DisposeAsync();
        }
    }

    [Fact]
    public void GetSessionInfo_ReturnsNull_WhenNoSessionExists()
    {
        var hubContext = Substitute.For<IHubContext<AgentHubHub>>();
        var manager = new PtySessionManager(hubContext, NullLogger<PtySessionManager>.Instance);

        var info = manager.GetSessionInfo(Guid.NewGuid());
        Assert.Null(info);
    }
}
