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
        var orchestrator = new ExecutionOrchestrator(convRepo, wsRepo, providerMgr, snapshotSvc, broadcaster, permService);

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
}
