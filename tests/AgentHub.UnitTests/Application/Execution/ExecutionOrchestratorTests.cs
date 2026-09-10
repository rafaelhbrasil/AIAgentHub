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
        var orchestrator = new ExecutionOrchestrator(convRepo, wsRepo, providerMgr, snapshotSvc, broadcaster, permService, tracker, new AIAgentHub.Domain.Configuration.CliExecutionOptions(), Substitute.For<IPromptLogger>());

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
        var orchestrator = new ExecutionOrchestrator(convRepo, wsRepo, providerMgr, snapshotSvc, broadcaster, permService, tracker, options, Substitute.For<IPromptLogger>());

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
        var orchestrator = new ExecutionOrchestrator(convRepo, wsRepo, providerMgr, snapshotSvc, broadcaster, permService, tracker, new AIAgentHub.Domain.Configuration.CliExecutionOptions(), Substitute.For<IPromptLogger>());

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
        var promptLogger = Substitute.For<AIAgentHub.Application.Providers.IPromptLogger>();

        var permService = new PermissionService(permRepo, broadcaster);
        var tracker = new ActiveExecutionTracker();
        var orchestrator = new ExecutionOrchestrator(convRepo, wsRepo, providerMgr, snapshotSvc, broadcaster, permService, tracker, new AIAgentHub.Domain.Configuration.CliExecutionOptions(), promptLogger);

        var ws = Workspace.Create("WS", Path.GetTempPath());
        var conv = Conversation.Create(ws.Id, "Conv", "testprovider", "model-1");

        // Add prior message from the SAME provider, but checkpoint at 0 (e.g. previous turn was aborted)
        _ = conv.AddMessage(MessageRole.User, "Prior Prompt", null, "testprovider", "model-1");
        _ = conv.AddMessage(MessageRole.Assistant, "Prior Answer", null, "testprovider", "model-1");
        _ = conv.AddOrUpdateProviderSession("testprovider", "sess-1", null, 0);

        _ = convRepo.GetByIdAsync(conv.Id, Arg.Any<CancellationToken>()).Returns(conv);
        _ = wsRepo.GetByIdAsync(ws.Id, Arg.Any<CancellationToken>()).Returns(ws);

        var provider = Substitute.For<IProvider>();
        _ = provider.Id.Returns("testprovider");
        _ = providerMgr.GetProvider("testprovider").Returns(provider);
        _ = provider.ExecuteAsync(Arg.Any<ProviderExecutionContext>()).Returns(Task.CompletedTask);

        await orchestrator.ExecuteAsync(conv.Id, "Current Prompt");

        // Should execute with clean "Current Prompt" without prepending Context Handoff
        await provider.Received(1).ExecuteAsync(Arg.Is<ProviderExecutionContext>(c =>
            c.Prompt == "Current Prompt" && !c.Prompt.Contains("[AI Agent Hub — Conversation Context Handoff]")));

        promptLogger.Received(1).LogPromptExecution(
            conv.Id,
            "testprovider",
            "model-1",
            "Current Prompt",
            false,
            Arg.Any<int>());
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
        var promptLogger = Substitute.For<AIAgentHub.Application.Providers.IPromptLogger>();

        var permService = new PermissionService(permRepo, broadcaster);
        var tracker = new ActiveExecutionTracker();
        var orchestrator = new ExecutionOrchestrator(convRepo, wsRepo, providerMgr, snapshotSvc, broadcaster, permService, tracker, new AIAgentHub.Domain.Configuration.CliExecutionOptions(), promptLogger);

        var ws = Workspace.Create("WS", Path.GetTempPath());
        var conv = Conversation.Create(ws.Id, "Conv", "targetprovider", "model-2");

        // Prior messages are from another provider ("sourceprovider")
        _ = conv.AddMessage(MessageRole.User, "Source Prompt", null, "sourceprovider", "model-1");
        _ = conv.AddMessage(MessageRole.Assistant, "Source Answer", null, "sourceprovider", "model-1");
        _ = conv.AddOrUpdateProviderSession("targetprovider", "sess-target", null, 0);

        _ = convRepo.GetByIdAsync(conv.Id, Arg.Any<CancellationToken>()).Returns(conv);
        _ = wsRepo.GetByIdAsync(ws.Id, Arg.Any<CancellationToken>()).Returns(ws);

        var targetProvider = Substitute.For<IProvider>();
        _ = targetProvider.Id.Returns("targetprovider");
        _ = providerMgr.GetProvider("targetprovider").Returns(targetProvider);
        _ = targetProvider.ExecuteAsync(Arg.Any<ProviderExecutionContext>()).Returns(Task.CompletedTask);

        await orchestrator.ExecuteAsync(conv.Id, "Target Prompt");

        // Should inject Context Handoff preamble because unshared messages are from "sourceprovider"
        await targetProvider.Received(1).ExecuteAsync(Arg.Is<ProviderExecutionContext>(c =>
            c.Prompt.Contains("[AI Agent Hub — Conversation Context Handoff]") &&
            c.Prompt.Contains("The user switched to you from another AI provider in this ongoing conversation") &&
            c.Prompt.Contains("Target Prompt")));

        promptLogger.Received(1).LogPromptExecution(
            conv.Id,
            "targetprovider",
            "model-2",
            Arg.Is<string>(p => p.Contains("[AI Agent Hub — Conversation Context Handoff]")),
            true,
            Arg.Is<int>(count => count >= 2));
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
        var orchestrator = new ExecutionOrchestrator(convRepo, wsRepo, providerMgr, snapshotSvc, broadcaster, permService, tracker, new AIAgentHub.Domain.Configuration.CliExecutionOptions(), Substitute.For<IPromptLogger>());

        var ws = Workspace.Create("WS", Path.GetTempPath());
        var conv = Conversation.Create(ws.Id, "Conv", "testprovider", "model-1");

        // Checkpoint already advanced to 4
        _ = conv.AddMessage(MessageRole.User, "1", null, "testprovider", "model-1");
        _ = conv.AddMessage(MessageRole.Assistant, "2", null, "testprovider", "model-1");
        _ = conv.AddMessage(MessageRole.User, "3", null, "testprovider", "model-1");
        _ = conv.AddMessage(MessageRole.Assistant, "4", null, "testprovider", "model-1");
        var session = conv.AddOrUpdateProviderSession("testprovider", "old-sess", null, 4);

        _ = convRepo.GetByIdAsync(conv.Id, Arg.Any<CancellationToken>()).Returns(conv);
        _ = wsRepo.GetByIdAsync(ws.Id, Arg.Any<CancellationToken>()).Returns(ws);

        var provider = Substitute.For<IProvider>();
        _ = provider.Id.Returns("testprovider");
        _ = providerMgr.GetProvider("testprovider").Returns(provider);

        _ = provider.ExecuteAsync(Arg.Any<ProviderExecutionContext>()).Returns(async x =>
        {
            var ctx = x.Arg<ProviderExecutionContext>();
            if (ctx.OnSessionCreated != null)
            {
                await ctx.OnSessionCreated("new-dynamic-session-id");
            }
        });

        await orchestrator.ExecuteAsync(conv.Id, "Prompt");

        // Ensure session ID is updated, but sequence index is NOT reset to 0
        Assert.Equal("new-dynamic-session-id", conv.ProviderSessionId);
        var updatedSession = conv.ProviderSessions.First(s => s.ProviderId == "testprovider");
        Assert.Equal("new-dynamic-session-id", updatedSession.ProviderSessionId);
        // It advanced to assistantMsg sequence index, certainly not 0!
        Assert.True(updatedSession.LastSharedSequenceIndex >= 4);
    }
}
