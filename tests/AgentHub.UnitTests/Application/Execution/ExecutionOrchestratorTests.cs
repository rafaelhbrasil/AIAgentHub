using AIAgentHub.Application.Conversations;
using AIAgentHub.Application.Execution;
using AIAgentHub.Application.FileChanges;
using AIAgentHub.Application.Providers;
using AIAgentHub.Application.Realtime;
using AIAgentHub.Domain.Conversations;
using AIAgentHub.Domain.Permissions;
using AIAgentHub.Domain.Repositories;
using AIAgentHub.Domain.Workspaces;

using NSubstitute;

namespace AgentHub.UnitTests.Application.Execution;

public sealed class ExecutionOrchestratorTests
{
    [Fact]
    public async Task ExecutionOrchestrator_ExecutionAndAbort_ShouldWork()
    {
        var convRepo = Substitute.For<IConversationRepository>();
        var wsRepo = Substitute.For<IWorkspaceRepository>();
        var providerMgr = Substitute.For<IProviderManager>();
        var broadcaster = Substitute.For<IAgentRealtimeBroadcaster>();
        var snapshotSvc = Substitute.For<ISnapshotService>();
        var permRepo = Substitute.For<IPermissionRequestRepository>();

        var permService = new PermissionService(permRepo, broadcaster);
        var tracker = new ActiveExecutionTracker();
        var orchestrator = new ExecutionOrchestrator(convRepo, wsRepo, providerMgr, snapshotSvc, broadcaster, permService, tracker, new AIAgentHub.Domain.Configuration.CliExecutionOptions());

        var ws = Workspace.Create("WS", Path.GetTempPath());
        var conv = Conversation.Create(ws.Id, "Conv Title", "testprovider", "model-1");

        _ = convRepo.GetByIdAsync(conv.Id, Arg.Any<CancellationToken>()).Returns(conv);
        _ = wsRepo.GetByIdAsync(ws.Id, Arg.Any<CancellationToken>()).Returns(ws);

        var provider = Substitute.For<IProvider>();
        _ = provider.Id.Returns("testprovider");
        _ = providerMgr.GetProvider("testprovider").Returns(provider);

        // Test ExecutionOrchestrator
        _ = provider.ExecuteAsync(Arg.Any<ProviderExecutionContext>()).Returns(Task.CompletedTask);

        await orchestrator.ExecuteAsync(conv.Id, "User Prompt");
        await provider.Received(1).ExecuteAsync(Arg.Is<ProviderExecutionContext>(c => c.Prompt == "User Prompt"));

        // Execution Exception Path
        _ = provider.ExecuteAsync(Arg.Any<ProviderExecutionContext>()).Returns(Task.FromException(new InvalidOperationException("CLI error")));
        await orchestrator.ExecuteAsync(conv.Id, "User Prompt");

        await orchestrator.AbortAsync(conv.Id);
        await provider.Received(1).AbortAsync(conv.Id);

        // Missing Conv / WS exceptions
        _ = await Assert.ThrowsAsync<KeyNotFoundException>(() => orchestrator.ExecuteAsync(Guid.NewGuid(), "prompt"));
    }

    [Fact]
    public async Task ExecutionOrchestrator_WhenTimeoutOccurs_ShouldAutoResumeWithContinuePrompt()
    {
        var convRepo = Substitute.For<IConversationRepository>();
        var wsRepo = Substitute.For<IWorkspaceRepository>();
        var providerMgr = Substitute.For<IProviderManager>();
        var broadcaster = Substitute.For<IAgentRealtimeBroadcaster>();
        var snapshotSvc = Substitute.For<ISnapshotService>();
        var permRepo = Substitute.For<IPermissionRequestRepository>();

        var options = new AIAgentHub.Domain.Configuration.CliExecutionOptions
        {
            AutoResumeOnTimeout = true,
            MaxAutoResumes = 2,
            TimeoutMinutes = 10
        };

        var permService = new PermissionService(permRepo, broadcaster);
        var tracker = new ActiveExecutionTracker();
        var orchestrator = new ExecutionOrchestrator(convRepo, wsRepo, providerMgr, snapshotSvc, broadcaster, permService, tracker, options);

        var ws = Workspace.Create("WS", Path.GetTempPath());
        var conv = Conversation.Create(ws.Id, "Conv Title", "testprovider", "model-1");

        _ = convRepo.GetByIdAsync(conv.Id, Arg.Any<CancellationToken>()).Returns(conv);
        _ = wsRepo.GetByIdAsync(ws.Id, Arg.Any<CancellationToken>()).Returns(ws);

        var provider = Substitute.For<IProvider>();
        _ = provider.Id.Returns("testprovider");
        _ = providerMgr.GetProvider("testprovider").Returns(provider);

        var callCount = 0;
        _ = provider.ExecuteAsync(Arg.Any<ProviderExecutionContext>()).Returns(x =>
        {
            callCount++;
            if (callCount == 1)
            {
                return Task.FromException(new TimeoutException("Operation timed out"));
            }
            return Task.CompletedTask;
        });

        await orchestrator.ExecuteAsync(conv.Id, "Initial Prompt");

        // Provider should be called twice: first with Initial Prompt, then auto-resumed with Continue prompt
        await provider.Received(1).ExecuteAsync(Arg.Is<ProviderExecutionContext>(c => c.Prompt == "Initial Prompt"));
        await provider.Received(1).ExecuteAsync(Arg.Is<ProviderExecutionContext>(c => c.Prompt == "Continue from where you left off."));

        // Only 1 user message should exist in the conversation (the continuation prompt must not be persisted as a user message)
        Assert.Single(conv.Messages, m => m.Role == MessageRole.User);
        Assert.Contains(conv.Messages, m => m.Role == MessageRole.Assistant);
    }

    [Fact]
    public async Task ExecutionOrchestrator_HeartbeatCallback_ShouldBroadcastHeartbeatEvent()
    {
        var convRepo = Substitute.For<IConversationRepository>();
        var wsRepo = Substitute.For<IWorkspaceRepository>();
        var providerMgr = Substitute.For<IProviderManager>();
        var broadcaster = Substitute.For<IAgentRealtimeBroadcaster>();
        var snapshotSvc = Substitute.For<ISnapshotService>();
        var permRepo = Substitute.For<IPermissionRequestRepository>();

        var permService = new PermissionService(permRepo, broadcaster);
        var tracker = new ActiveExecutionTracker();
        var orchestrator = new ExecutionOrchestrator(convRepo, wsRepo, providerMgr, snapshotSvc, broadcaster, permService, tracker, new AIAgentHub.Domain.Configuration.CliExecutionOptions());

        var ws = Workspace.Create("WS", Path.GetTempPath());
        var conv = Conversation.Create(ws.Id, "Conv Title", "testprovider", "model-1");

        _ = convRepo.GetByIdAsync(conv.Id, Arg.Any<CancellationToken>()).Returns(conv);
        _ = wsRepo.GetByIdAsync(ws.Id, Arg.Any<CancellationToken>()).Returns(ws);

        var provider = Substitute.For<IProvider>();
        _ = provider.Id.Returns("testprovider");
        _ = providerMgr.GetProvider("testprovider").Returns(provider);

        _ = provider.ExecuteAsync(Arg.Any<ProviderExecutionContext>()).Returns(async x =>
        {
            var ctx = x.Arg<ProviderExecutionContext>();
            if (ctx.OnHeartbeat != null)
            {
                await ctx.OnHeartbeat("Still thinking...", 60);
            }
        });

        await orchestrator.ExecuteAsync(conv.Id, "Prompt");

        await broadcaster.Received(1).SendConversationEventAsync(
            "conversation.heartbeat",
            conv.Id,
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ActiveExecutionTracker_ShouldPreventDuplicateExecutions()
    {
        var tracker = new ActiveExecutionTracker();
        var convId = Guid.NewGuid();

        using var cts1 = new CancellationTokenSource();
        using var cts2 = new CancellationTokenSource();

        Assert.False(tracker.IsExecuting(convId));
        Assert.True(tracker.TryStartExecution(convId, cts1));
        Assert.True(tracker.IsExecuting(convId));

        // Duplicate start fails
        Assert.False(tracker.TryStartExecution(convId, cts2));

        Assert.True(tracker.TryGetCancellationTokenSource(convId, out var registeredCts));
        Assert.Same(cts1, registeredCts);

        // Completion frees the lock
        Assert.True(tracker.CompleteExecution(convId));
        Assert.False(tracker.IsExecuting(convId));

        // Now can start again
        Assert.True(tracker.TryStartExecution(convId, cts2));
        Assert.True(tracker.IsExecuting(convId));
        Assert.True(tracker.CompleteExecution(convId));
    }

    [Fact]
    public async Task ExecutionOrchestrator_WhenSameProviderHasUnsharedMessages_ShouldNotInjectHandoffPreamble()
    {
        var convRepo = Substitute.For<IConversationRepository>();
        var wsRepo = Substitute.For<IWorkspaceRepository>();
        var providerMgr = Substitute.For<IProviderManager>();
        var broadcaster = Substitute.For<IAgentRealtimeBroadcaster>();
        var snapshotSvc = Substitute.For<ISnapshotService>();
        var permRepo = Substitute.For<IPermissionRequestRepository>();

        var permService = new PermissionService(permRepo, broadcaster);
        var tracker = new ActiveExecutionTracker();
        var orchestrator = new ExecutionOrchestrator(convRepo, wsRepo, providerMgr, snapshotSvc, broadcaster, permService, tracker, new AIAgentHub.Domain.Configuration.CliExecutionOptions());

        var ws = Workspace.Create("WS", Path.GetTempPath());
        var conv = Conversation.Create(ws.Id, "Conv Title", "testprovider", "model-1");

        // Prior messages from SAME provider
        _ = conv.AddMessage(MessageRole.User, "Hello turn 1", null, "testprovider", "model-1");
        _ = conv.AddMessage(MessageRole.Assistant, "Hi from testprovider", null, "testprovider", "model-1");

        _ = convRepo.GetByIdAsync(conv.Id, Arg.Any<CancellationToken>()).Returns(conv);
        _ = wsRepo.GetByIdAsync(ws.Id, Arg.Any<CancellationToken>()).Returns(ws);

        var provider = Substitute.For<IProvider>();
        _ = provider.Id.Returns("testprovider");
        _ = providerMgr.GetProvider("testprovider").Returns(provider);

        ProviderExecutionContext? capturedContext = null;
        _ = provider.ExecuteAsync(Arg.Do<ProviderExecutionContext>(ctx => capturedContext = ctx))
            .Returns(Task.CompletedTask);

        await orchestrator.ExecuteAsync(conv.Id, "New turn 2");

        Assert.NotNull(capturedContext);
        Assert.DoesNotContain("[AI Agent Hub — Conversation Context Handoff]", capturedContext!.Prompt);
        Assert.Equal("New turn 2", capturedContext.Prompt);
    }

    [Fact]
    public async Task ExecutionOrchestrator_WhenForeignProviderHasUnsharedMessages_ShouldInjectHandoffPreamble()
    {
        var convRepo = Substitute.For<IConversationRepository>();
        var wsRepo = Substitute.For<IWorkspaceRepository>();
        var providerMgr = Substitute.For<IProviderManager>();
        var broadcaster = Substitute.For<IAgentRealtimeBroadcaster>();
        var snapshotSvc = Substitute.For<ISnapshotService>();
        var permRepo = Substitute.For<IPermissionRequestRepository>();

        var permService = new PermissionService(permRepo, broadcaster);
        var tracker = new ActiveExecutionTracker();
        var orchestrator = new ExecutionOrchestrator(convRepo, wsRepo, providerMgr, snapshotSvc, broadcaster, permService, tracker, new AIAgentHub.Domain.Configuration.CliExecutionOptions());

        var ws = Workspace.Create("WS", Path.GetTempPath());
        var conv = Conversation.Create(ws.Id, "Conv Title", "testprovider", "model-1");

        // Prior unshared turn was from a FOREIGN provider ("claude")
        _ = conv.AddMessage(MessageRole.User, "Hello turn 1", null, "claude", "claude-3-7-sonnet");
        _ = conv.AddMessage(MessageRole.Assistant, "Hi from claude", null, "claude", "claude-3-7-sonnet");

        _ = convRepo.GetByIdAsync(conv.Id, Arg.Any<CancellationToken>()).Returns(conv);
        _ = wsRepo.GetByIdAsync(ws.Id, Arg.Any<CancellationToken>()).Returns(ws);

        var provider = Substitute.For<IProvider>();
        _ = provider.Id.Returns("testprovider");
        _ = providerMgr.GetProvider("testprovider").Returns(provider);

        ProviderExecutionContext? capturedContext = null;
        _ = provider.ExecuteAsync(Arg.Do<ProviderExecutionContext>(ctx => capturedContext = ctx))
            .Returns(Task.CompletedTask);

        await orchestrator.ExecuteAsync(conv.Id, "New turn 2");

        Assert.NotNull(capturedContext);
        Assert.Contains("[AI Agent Hub — Conversation Context Handoff]", capturedContext!.Prompt);
        Assert.Contains("Hello turn 1", capturedContext.Prompt);
        Assert.Contains("Hi from claude", capturedContext.Prompt);
        Assert.Contains("New turn 2", capturedContext.Prompt);
    }

    [Fact]
    public async Task ExecutionOrchestrator_WhenSessionDiscovered_ShouldPreserveLastSharedSequenceIndex()
    {
        var convRepo = Substitute.For<IConversationRepository>();
        var wsRepo = Substitute.For<IWorkspaceRepository>();
        var providerMgr = Substitute.For<IProviderManager>();
        var broadcaster = Substitute.For<IAgentRealtimeBroadcaster>();
        var snapshotSvc = Substitute.For<ISnapshotService>();
        var permRepo = Substitute.For<IPermissionRequestRepository>();

        var permService = new PermissionService(permRepo, broadcaster);
        var tracker = new ActiveExecutionTracker();
        var orchestrator = new ExecutionOrchestrator(convRepo, wsRepo, providerMgr, snapshotSvc, broadcaster, permService, tracker, new AIAgentHub.Domain.Configuration.CliExecutionOptions());

        var ws = Workspace.Create("WS", Path.GetTempPath());
        var conv = Conversation.Create(ws.Id, "Conv", "testprovider", "model-1");

        // Checkpoint already advanced to 4
        _ = conv.AddMessage(MessageRole.User, "1", null, "testprovider", "model-1");
        _ = conv.AddMessage(MessageRole.Assistant, "2", null, "testprovider", "model-1");
        _ = conv.AddMessage(MessageRole.User, "3", null, "testprovider", "model-1");
        var msg4 = conv.AddMessage(MessageRole.Assistant, "4", null, "testprovider", "model-1");
        _ = conv.AddOrUpdateProviderSession("testprovider", "initial-session", msg4.Id, 4);

        _ = convRepo.GetByIdAsync(conv.Id, Arg.Any<CancellationToken>()).Returns(conv);
        _ = wsRepo.GetByIdAsync(ws.Id, Arg.Any<CancellationToken>()).Returns(ws);

        var provider = Substitute.For<IProvider>();
        _ = provider.Id.Returns("testprovider");
        _ = providerMgr.GetProvider("testprovider").Returns(provider);

        // During execution, provider discovers a new session ID
        _ = provider.ExecuteAsync(Arg.Any<ProviderExecutionContext>()).Returns(async callInfo =>
        {
            var ctx = callInfo.Arg<ProviderExecutionContext>();
            if (ctx.OnSessionCreated != null)
            {
                await ctx.OnSessionCreated("discovered-new-session");
            }
        });

        await orchestrator.ExecuteAsync(conv.Id, "5");

        var session = conv.ProviderSessions.First(s => s.ProviderId == "testprovider");
        Assert.Equal("discovered-new-session", session.ProviderSessionId);
        // Ensure LastSharedSequenceIndex was NOT reset to 0
        Assert.True(session.LastSharedSequenceIndex >= 4);
    }
}
