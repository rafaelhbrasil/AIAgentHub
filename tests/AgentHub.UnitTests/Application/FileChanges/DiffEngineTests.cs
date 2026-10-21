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

    [Fact]
    public void DiffEngine_CalculateTextDiff_WhenOldTextIsNull_BuildsAllAddedDiff()
    {
        var engine = new DiffEngine();
        var newText = "Brand new file\nSecond line";

        var diff = engine.CalculateTextDiff("newfile.txt", null, newText);

        Assert.True(diff.HasChanges);
        Assert.Equal(2, diff.AdditionsCount);
        Assert.Equal(0, diff.DeletionsCount);
        Assert.Equal(2, diff.UnifiedLines.Count);
        Assert.All(diff.UnifiedLines, l => Assert.Equal(DiffLineKind.Added, l.Kind));
    }

    [Fact]
    public void DiffEngine_CalculateTextDiff_WhenNewTextIsNull_BuildsAllDeletedDiff()
    {
        var engine = new DiffEngine();
        var oldText = "File to delete\nSecond line";

        var diff = engine.CalculateTextDiff("deleted.txt", oldText, null);

        Assert.True(diff.HasChanges);
        Assert.Equal(0, diff.AdditionsCount);
        Assert.Equal(2, diff.DeletionsCount);
        Assert.Equal(2, diff.UnifiedLines.Count);
        Assert.All(diff.UnifiedLines, l => Assert.Equal(DiffLineKind.Deleted, l.Kind));
    }

    [Fact]
    public void DiffEngine_CalculateTextDiff_WhenIdentical_HasZeroChanges()
    {
        var engine = new DiffEngine();
        var text = "Same line 1\nSame line 2";

        var diff = engine.CalculateTextDiff("same.txt", text, text);

        Assert.False(diff.HasChanges);
        Assert.Equal(0, diff.AdditionsCount);
        Assert.Equal(0, diff.DeletionsCount);
        Assert.Equal(2, diff.UnifiedLines.Count);
        Assert.All(diff.UnifiedLines, l => Assert.Equal(DiffLineKind.Unchanged, l.Kind));
    }

    [Fact]
    public void DiffEngine_CalculateImageDiff_DetectsDifferencesAndUnchanged()
    {
        var engine = new DiffEngine();

        var changedDiff = engine.CalculateImageDiff("logo.png", "data:image/png;base64,AAA", "data:image/png;base64,BBB");
        Assert.True(changedDiff.IsBinary);
        Assert.True(changedDiff.HasChanges);
        Assert.Equal(1, changedDiff.AdditionsCount);
        Assert.Equal(1, changedDiff.DeletionsCount);

        var unchangedDiff = engine.CalculateImageDiff("logo.png", "data:image/png;base64,SAME", "data:image/png;base64,SAME");
        Assert.True(unchangedDiff.IsBinary);
        Assert.False(unchangedDiff.HasChanges);
        Assert.Equal(0, unchangedDiff.AdditionsCount);
        Assert.Equal(0, unchangedDiff.DeletionsCount);
    }
}
