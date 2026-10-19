using AIAgentHub.Infrastructure.Realtime;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;

namespace AgentHub.UnitTests.Infrastructure.Realtime;

public class SignalRAgentRealtimeBroadcasterTests
{
    private readonly IHubContext<AgentHubHub> _hubContext = Substitute.For<IHubContext<AgentHubHub>>();
    private readonly IHubClients _hubClients = Substitute.For<IHubClients>();
    private readonly IClientProxy _groupProxy = Substitute.For<IClientProxy>();
    private readonly IClientProxy _userProxy = Substitute.For<IClientProxy>();
    private readonly IClientProxy _allProxy = Substitute.For<IClientProxy>();

    public SignalRAgentRealtimeBroadcasterTests()
    {
        _hubContext.Clients.Returns(_hubClients);
        _hubClients.Group(Arg.Any<string>()).Returns(_groupProxy);
        _hubClients.User(Arg.Any<string>()).Returns(_userProxy);
        _hubClients.All.Returns(_allProxy);
    }

    [Fact]
    public void GetGroupName_WithUserId_ReturnsScopedFormat()
    {
        var convId = Guid.NewGuid();
        var group = AgentHubHub.GetGroupName("User123", convId);
        Assert.Equal($"usr_user123_conv_{convId}", group);
    }

    [Fact]
    public void GetGroupName_WithoutUserId_ReturnsFallbackFormat()
    {
        var convId = Guid.NewGuid();
        var group = AgentHubHub.GetGroupName(null, convId);
        Assert.Equal($"conv_{convId}", group);

        var emptyGroup = AgentHubHub.GetGroupName("   ", convId);
        Assert.Equal($"conv_{convId}", emptyGroup);
    }

    [Fact]
    public async Task SendMessageStreamChunkAsync_WithUserId_TargetsUserScopedGroup()
    {
        var broadcaster = new SignalRAgentRealtimeBroadcaster(_hubContext);
        var convId = Guid.NewGuid();

        await broadcaster.SendMessageStreamChunkAsync(convId, "hello", "User42");

        _hubClients.Received(1).Group($"usr_user42_conv_{convId}");
        await _groupProxy.Received(1).SendCoreAsync(
            "streamChunk",
            Arg.Is<object?[]>(args => args.Length == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendConversationEventAsync_WithUserId_TargetsUserScopedGroup()
    {
        var broadcaster = new SignalRAgentRealtimeBroadcaster(_hubContext);
        var convId = Guid.NewGuid();

        await broadcaster.SendConversationEventAsync("conversation.started", convId, new { status = "active" }, "User42");

        _hubClients.Received(1).Group($"usr_user42_conv_{convId}");
        await _groupProxy.Received(1).SendCoreAsync(
            "conversationEvent",
            Arg.Is<object?[]>(args => args.Length == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendNotificationAsync_WithUserId_TargetsUser()
    {
        var broadcaster = new SignalRAgentRealtimeBroadcaster(_hubContext);

        await broadcaster.SendNotificationAsync("info", "Test Title", "Test Message", "User42");

        _hubClients.Received(1).User("User42");
        await _userProxy.Received(1).SendCoreAsync(
            "notification",
            Arg.Is<object?[]>(args => args.Length == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendNotificationAsync_WithoutUserId_TargetsAll()
    {
        var broadcaster = new SignalRAgentRealtimeBroadcaster(_hubContext);

        await broadcaster.SendNotificationAsync("info", "Test Title", "Test Message");

        _ = _hubClients.Received(1).All;
        await _allProxy.Received(1).SendCoreAsync(
            "notification",
            Arg.Is<object?[]>(args => args.Length == 1),
            Arg.Any<CancellationToken>());
    }
}
