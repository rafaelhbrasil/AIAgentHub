using AIAgentHub.Application.Execution;
using AIAgentHub.Application.Realtime;
using AIAgentHub.Domain.Conversations;
using AIAgentHub.Domain.Permissions;
using AIAgentHub.Domain.Repositories;

using NSubstitute;

namespace AgentHub.UnitTests.Application.Execution;

public sealed class PermissionServiceTests
{
    [Fact]
    public async Task PermissionService_Operations_ShouldWork()
    {
        var permRepo = Substitute.For<IPermissionRequestRepository>();
        var broadcaster = Substitute.For<IAgentRealtimeBroadcaster>();
        var convId = Guid.NewGuid();

        var permService = new PermissionService(permRepo, broadcaster);

        _ = permRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(PermissionRequest.Create(convId, "testprovider", PermissionType.FileWrite, "target", "reason"));

        var req = await permService.RequestPermissionAsync(convId, "testprovider", PermissionType.FileWrite, "target", "reason");
        Assert.NotNull(req);

        var decided = await permService.DecideAsync(req.Id, true);
        Assert.Equal(PermissionDecision.Approved, decided.Decision);

        _ = permRepo.GetByConversationIdAsync(convId, Arg.Any<CancellationToken>())
            .Returns(new List<PermissionRequest> { req });
        var reqList = await permService.GetRequestsByConversationAsync(convId);
        _ = Assert.Single(reqList);
    }
}
