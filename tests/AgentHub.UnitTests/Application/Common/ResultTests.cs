using AIAgentHub.Application.Common;

namespace AgentHub.UnitTests.Application.Common;

public sealed class ResultTests
{
    [Fact]
    public void Result_And_GenericResult_ShouldBehaveCorrectly()
    {
        var ok = Result.Ok();
        Assert.True(ok.Success);
        Assert.Null(ok.Error);

        var fail = Result.Fail("Failed error");
        Assert.False(fail.Success);
        Assert.Equal("Failed error", fail.Error);

        var genericOk = Result<string>.Ok("data payload");
        Assert.True(genericOk.Success);
        Assert.Equal("data payload", genericOk.Data);
        Assert.Null(genericOk.Error);

        var genericFail = Result<int>.Fail("invalid number");
        Assert.False(genericFail.Success);
        Assert.Equal(0, genericFail.Data);
        Assert.Equal("invalid number", genericFail.Error);
    }
}
