using AIAgentHub.Application.Conversations;
using AIAgentHub.Application.Providers;
using AIAgentHub.Application.Realtime;
using AIAgentHub.Domain.Conversations;
using AIAgentHub.Domain.Providers;
using AIAgentHub.Domain.Repositories;
using AIAgentHub.Domain.Workspaces;
using NSubstitute;
using Xunit;

namespace AgentHub.UnitTests.Application;

public class ConversationSwitchServiceTests
{
    private readonly IConversationRepository _conversationRepository = Substitute.For<IConversationRepository>();
    private readonly IWorkspaceRepository _workspaceRepository = Substitute.For<IWorkspaceRepository>();
    private readonly IProviderManager _providerManager = Substitute.For<IProviderManager>();
    private readonly IAgentRealtimeBroadcaster _broadcaster = Substitute.For<IAgentRealtimeBroadcaster>();
    private readonly IProvider _claudeProvider = Substitute.For<IProvider>();
    private readonly IProvider _geminiProvider = Substitute.For<IProvider>();

    public ConversationSwitchServiceTests()
    {
        _claudeProvider.Id.Returns("claude-code");
        _claudeProvider.StartSessionAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns("claude-sess-1");

        _geminiProvider.Id.Returns("gemini");
        _geminiProvider.StartSessionAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns("gemini-sess-1");

        _providerManager.GetProvider("claude-code").Returns(_claudeProvider);
        _providerManager.GetProvider("gemini").Returns(_geminiProvider);

        _providerManager.GetProviderInfoAsync("claude-code", Arg.Any<CancellationToken>())
            .Returns(new ProviderInfo { Id = "claude-code", DisplayName = "Claude Code", Status = ProviderStatus.Ready, IsInstalled = true, IsAuthenticated = true });
        _providerManager.GetProviderInfoAsync("gemini", Arg.Any<CancellationToken>())
            .Returns(new ProviderInfo { Id = "gemini", DisplayName = "Gemini CLI", Status = ProviderStatus.Ready, IsInstalled = true, IsAuthenticated = true });
    }

    [Fact]
    public async Task SwitchProviderAsync_NewProvider_SwitchesAndCreatesSession()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = Workspace.Create("Test WS", "D:\\TestPath");
        var conversation = Conversation.Create(workspaceId, "Test Conv", "gemini");
        _ = conversation.AddMessage(MessageRole.User, "First prompt");
        _ = conversation.AddMessage(MessageRole.Assistant, "First response", null, "gemini");

        _conversationRepository.GetByIdAsync(conversation.Id, Arg.Any<CancellationToken>()).Returns(conversation);
        _workspaceRepository.GetByIdAsync(workspaceId, Arg.Any<CancellationToken>()).Returns(workspace);

        var service = new ConversationSwitchService(_conversationRepository, _workspaceRepository, _providerManager, _broadcaster);

        var request = new SwitchProviderRequest("claude-code", "claude-3-7-sonnet", "all");
        var result = await service.SwitchProviderAsync(conversation.Id, request);

        Assert.Equal(conversation.Id, result.ConversationId);
        Assert.Equal("claude-code", result.ActiveProviderId);
        Assert.Equal("claude-3-7-sonnet", result.ActiveModelId);
        Assert.Equal(1, result.MigratedMessageCount); // 1 interaction turn (user prompt + assistant response)
        Assert.Equal("claude-sess-1", result.TargetSessionId);
        Assert.Equal(ConversationStatus.Active, conversation.Status);
        Assert.Single(conversation.ProviderSessions);

        await _broadcaster.Received(1).SendConversationEventAsync(
            "conversation.switched_provider",
            conversation.Id,
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SwitchProviderAsync_DeltaScope_CalculatesUnsharedMessages()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = Workspace.Create("Test WS", "D:\\TestPath");
        var conversation = Conversation.Create(workspaceId, "Test Conv", "gemini");
        var msg1 = conversation.AddMessage(MessageRole.User, "First prompt");
        var msg2 = conversation.AddMessage(MessageRole.Assistant, "First response", null, "gemini");

        // Existing session for claude-code was synced up to msg2 (seq 2)
        _ = conversation.AddOrUpdateProviderSession("claude-code", "existing-claude-sess", msg2.Id, 2);

        // Add 2 more messages under gemini (1 interaction)
        var msg3 = conversation.AddMessage(MessageRole.User, "Second prompt");
        var msg4 = conversation.AddMessage(MessageRole.Assistant, "Second response", null, "gemini");

        _conversationRepository.GetByIdAsync(conversation.Id, Arg.Any<CancellationToken>()).Returns(conversation);
        _workspaceRepository.GetByIdAsync(workspaceId, Arg.Any<CancellationToken>()).Returns(workspace);

        var service = new ConversationSwitchService(_conversationRepository, _workspaceRepository, _providerManager, _broadcaster);

        var request = new SwitchProviderRequest("claude-code", null, "delta");
        var result = await service.SwitchProviderAsync(conversation.Id, request);

        Assert.Equal(1, result.MigratedMessageCount); // 1 new interaction turn (msg3 + msg4)
        Assert.Equal("existing-claude-sess", result.TargetSessionId);

        var session = conversation.ProviderSessions.First(s => s.ProviderId == "claude-code");
        Assert.Equal(2, session.LastSharedSequenceIndex);
    }

    [Fact]
    public async Task SwitchProviderAsync_NoneScope_DoesNotAdvanceCheckpoint()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = Workspace.Create("Test WS", "D:\\TestPath");
        var conversation = Conversation.Create(workspaceId, "Test Conv", "gemini");
        var msg1 = conversation.AddMessage(MessageRole.User, "First prompt");
        var msg2 = conversation.AddMessage(MessageRole.Assistant, "First response", null, "gemini");

        // Existing session for claude-code was synced up to msg2 (seq 2)
        _ = conversation.AddOrUpdateProviderSession("claude-code", "existing-claude-sess", msg2.Id, 2);

        // Add 2 more messages under gemini
        var msg3 = conversation.AddMessage(MessageRole.User, "Second prompt");
        var msg4 = conversation.AddMessage(MessageRole.Assistant, "Second response", null, "gemini");

        _conversationRepository.GetByIdAsync(conversation.Id, Arg.Any<CancellationToken>()).Returns(conversation);
        _workspaceRepository.GetByIdAsync(workspaceId, Arg.Any<CancellationToken>()).Returns(workspace);

        var service = new ConversationSwitchService(_conversationRepository, _workspaceRepository, _providerManager, _broadcaster);

        var request = new SwitchProviderRequest("claude-code", null, "none");
        var result = await service.SwitchProviderAsync(conversation.Id, request);

        Assert.Equal(0, result.MigratedMessageCount);
        Assert.Equal("existing-claude-sess", result.TargetSessionId);

        // Checkpoint must NOT have advanced to 4
        var session = conversation.ProviderSessions.First(s => s.ProviderId == "claude-code");
        Assert.Equal(2, session.LastSharedSequenceIndex);
    }

    [Fact]
    public async Task AbortSwitchAsync_InSwitchingStatus_ResetsToActiveAndBroadcasts()
    {
        var workspaceId = Guid.NewGuid();
        var conversation = Conversation.Create(workspaceId, "Switching Conv", "gemini");
        conversation.SetStatus(ConversationStatus.SwitchingProvider);

        _conversationRepository.GetByIdAsync(conversation.Id, Arg.Any<CancellationToken>()).Returns(conversation);

        var service = new ConversationSwitchService(_conversationRepository, _workspaceRepository, _providerManager, _broadcaster);
        var result = await service.AbortSwitchAsync(conversation.Id);

        Assert.Equal(ConversationStatus.Active, conversation.Status);
        Assert.Equal(ConversationStatus.Active, result.Status);

        await _broadcaster.Received(1).SendConversationEventAsync(
            "conversation.status_changed",
            conversation.Id,
            Arg.Is<object>(o => o.ToString()!.Contains("Active")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SwitchProviderAsync_DiscontinuedOrNotReadyProvider_ThrowsInvalidOperationException()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = Workspace.Create("Test WS", "D:\\TestPath");
        var conversation = Conversation.Create(workspaceId, "Test Conv", "antigravity");

        _conversationRepository.GetByIdAsync(conversation.Id, Arg.Any<CancellationToken>()).Returns(conversation);
        _workspaceRepository.GetByIdAsync(workspaceId, Arg.Any<CancellationToken>()).Returns(workspace);

        var discontinuedProvider = Substitute.For<IProvider>();
        discontinuedProvider.Id.Returns("discontinued-prov");
        _providerManager.GetProvider("discontinued-prov").Returns(discontinuedProvider);
        _providerManager.GetProviderInfoAsync("discontinued-prov", Arg.Any<CancellationToken>())
            .Returns(new ProviderInfo { Id = "discontinued-prov", DisplayName = "Discontinued Provider", Status = ProviderStatus.Discontinued });

        var service = new ConversationSwitchService(_conversationRepository, _workspaceRepository, _providerManager, _broadcaster);

        var request = new SwitchProviderRequest("discontinued-prov", null, "all");
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SwitchProviderAsync(conversation.Id, request));
    }

    [Fact]
    public async Task SwitchProviderAsync_HiddenProvider_ThrowsInvalidOperationException()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = Workspace.Create("Test WS", "D:\\TestPath");
        var conversation = Conversation.Create(workspaceId, "Test Conv", "antigravity");

        _conversationRepository.GetByIdAsync(conversation.Id, Arg.Any<CancellationToken>()).Returns(conversation);
        _workspaceRepository.GetByIdAsync(workspaceId, Arg.Any<CancellationToken>()).Returns(workspace);

        var hiddenProvider = Substitute.For<IProvider>();
        hiddenProvider.Id.Returns("hidden-prov");
        _providerManager.GetProvider("hidden-prov").Returns(hiddenProvider);
        _providerManager.GetProviderInfoAsync("hidden-prov", Arg.Any<CancellationToken>())
            .Returns(new ProviderInfo { Id = "hidden-prov", DisplayName = "Hidden Provider", Status = ProviderStatus.Ready, IsHidden = true });

        var service = new ConversationSwitchService(_conversationRepository, _workspaceRepository, _providerManager, _broadcaster);

        var request = new SwitchProviderRequest("hidden-prov", null, "all");
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SwitchProviderAsync(conversation.Id, request));
    }

    [Fact]
    public async Task SwitchProviderAsync_LegacyChatWithoutSessionsOrSequenceIndexes_SwitchesSuccessfully()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = Workspace.Create("Legacy WS", "D:\\LegacyPath");
        var conversation = Conversation.Create(workspaceId, "Legacy Conv", "gemini");

        // Simulate legacy messages where SequenceIndex is 0
        var msg1 = Message.Create(conversation.Id, MessageRole.User, "Legacy prompt 1", null, 0, null, null);
        var msg2 = Message.Create(conversation.Id, MessageRole.Assistant, "Legacy response 1", null, 0, null, null);

        // Reflection or direct addition to simulate legacy EF-loaded state
        typeof(Conversation).GetField("_messages", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(conversation, new List<Message> { msg1, msg2 });

        Assert.Empty(conversation.ProviderSessions);

        _conversationRepository.GetByIdAsync(conversation.Id, Arg.Any<CancellationToken>()).Returns(conversation);
        _workspaceRepository.GetByIdAsync(workspaceId, Arg.Any<CancellationToken>()).Returns(workspace);

        var service = new ConversationSwitchService(_conversationRepository, _workspaceRepository, _providerManager, _broadcaster);

        var request = new SwitchProviderRequest("claude-code", "claude-3-7-sonnet", "all");
        var result = await service.SwitchProviderAsync(conversation.Id, request);

        Assert.Equal(conversation.Id, result.ConversationId);
        Assert.Equal("claude-code", result.ActiveProviderId);
        Assert.Equal(1, result.MigratedMessageCount); // 1 interaction turn
        Assert.Equal("claude-sess-1", result.TargetSessionId);
        Assert.Single(conversation.ProviderSessions);
    }
}
