using AIAgentHub.Application.Common;
using AIAgentHub.Application.Security;

namespace AgentHub.UnitTests.Application.Security;

public sealed class SetupServiceTests
{
    [Fact]
    public async Task SetupService_WipeAllDataAsync_ShouldInvokeDatabaseResetter()
    {
        var resetter = new TestDatabaseResetter();
        var setupService = new SetupService(null!, null!, null!, resetter);

        var result = await setupService.WipeAllDataAsync();

        Assert.True(result);
        Assert.True(resetter.WasWiped);
    }

    private sealed class TestDatabaseResetter : IDatabaseResetter
    {
        public bool WasWiped { get; private set; }
        public Task WipeAllDataAsync(CancellationToken cancellationToken = default)
        {
            WasWiped = true;
            return Task.CompletedTask;
        }
    }
}
