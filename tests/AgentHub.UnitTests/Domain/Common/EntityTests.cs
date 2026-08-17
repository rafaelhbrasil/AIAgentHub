using AIAgentHub.Domain.Common;

namespace AgentHub.UnitTests.Domain.Common;

public sealed class EntityTests
{
    private class TestEntity : Entity { }
    private class TestAggregateRoot : AggregateRoot { }

    [Fact]
    public void Entity_And_AggregateRoot_ShouldInitializeWithGuid()
    {
        var entity = new TestEntity();
        var root = new TestAggregateRoot();

        Assert.NotEqual(Guid.Empty, entity.Id);
        Assert.NotEqual(Guid.Empty, root.Id);
    }
}
