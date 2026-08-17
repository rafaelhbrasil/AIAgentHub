using AIAgentHub.Application.FileChanges;

namespace AgentHub.UnitTests.Application.FileChanges;

public sealed class DiffEngineTests
{
    [Fact]
    public void DiffEngine_CalculateTextDiff_ShouldDetectAdditionsAndDeletions()
    {
        var engine = new DiffEngine();
        var oldText = "Line 1\nLine 2\nLine 3";
        var newText = "Line 1\nLine 2 modified\nLine 3\nLine 4 added";

        var diff = engine.CalculateTextDiff("test.txt", oldText, newText);

        Assert.True(diff.HasChanges);
        Assert.True(diff.AdditionsCount > 0);
        Assert.NotEmpty(diff.UnifiedLines);
        Assert.NotEmpty(diff.SideBySideLines);
    }
}
