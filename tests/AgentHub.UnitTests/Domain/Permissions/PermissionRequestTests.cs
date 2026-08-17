using AIAgentHub.Domain.Permissions;

namespace AgentHub.UnitTests.Domain.Permissions;

public sealed class PermissionRequestTests
{
    [Fact]
    public void PermissionRequest_Create_And_Decide()
    {
        var convId = Guid.NewGuid();
        var req = PermissionRequest.Create(convId, "gemini", PermissionType.FileWrite, "src/main.cs", "modifying code");

        Assert.Equal(convId, req.ConversationId);
        Assert.Equal("gemini", req.ProviderId);
        Assert.Equal(PermissionType.FileWrite, req.Type);
        Assert.Equal("src/main.cs", req.Target);
        Assert.Equal("modifying code", req.Reason);
        Assert.Equal(PermissionDecision.Pending, req.Decision);
        Assert.Null(req.DecidedAtUtc);

        req.Decide(true);
        Assert.Equal(PermissionDecision.Approved, req.Decision);
        _ = Assert.NotNull(req.DecidedAtUtc);

        req.Decide(false);
        Assert.Equal(PermissionDecision.Denied, req.Decision);

        var fallbackReq = PermissionRequest.Create(convId, null!, PermissionType.DirectoryAccess, null!, null!);
        Assert.Equal(string.Empty, fallbackReq.ProviderId);
        Assert.Equal(string.Empty, fallbackReq.Target);
        Assert.Equal(string.Empty, fallbackReq.Reason);
    }
}
