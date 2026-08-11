using AIAgentHub.Application.FileChanges;
using AIAgentHub.Application.Rendering;
using Xunit;

namespace AIAgentHub.Application.Tests;

public sealed class ApplicationTests
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
    public async Task MarkdownRenderer_ShouldRenderHtmlAndCodeBlocks()
    {
        var renderer = new MarkdownContentRenderer();
        var md = "# Header 1\n\n```csharp\nvar x = 10;\n```\n\n- item 1\n- item 2";
        var bytes = System.Text.Encoding.UTF8.GetBytes(md);

        var result = await renderer.RenderAsync("readme.md", bytes);

        Assert.NotNull(result);
        Assert.Equal("text/markdown", result.ContentType);
        Assert.Contains("Header 1", result.RenderedHtml);
        Assert.Contains("var x = 10;", result.RenderedHtml);
    }

    [Fact]
    public async Task JsonRenderer_ShouldFormatAndIndentJson()
    {
        var renderer = new JsonContentRenderer();
        var json = "{\"name\":\"AgentHub\",\"version\":\"0.1\"}";
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);

        var result = await renderer.RenderAsync("appsettings.json", bytes);

        Assert.NotNull(result);
        Assert.Equal("application/json", result.ContentType);
        Assert.Contains("AgentHub", result.RenderedHtml);
    }

    [Fact]
    public async Task ContentRenderingManager_ShouldSelectAppropriateRenderer()
    {
        var renderers = new List<IContentRenderer>
        {
            new TextContentRenderer(),
            new MarkdownContentRenderer(),
            new JsonContentRenderer(),
            new XmlContentRenderer(),
            new YamlContentRenderer()
        };
        var manager = new ContentRenderingManager(renderers);

        var mdBytes = System.Text.Encoding.UTF8.GetBytes("# Title");
        var result = await manager.RenderFileAsync("docs.md", mdBytes);
        Assert.Equal("MarkdownRenderer", result.RendererName);
    }

    [Fact]
    public async Task SetupService_WipeAllDataAsync_ShouldInvokeDatabaseResetter()
    {
        var resetter = new TestDatabaseResetter();
        var setupService = new AIAgentHub.Application.Security.SetupService(
            null!, null!, null!, resetter);

        var result = await setupService.WipeAllDataAsync();

        Assert.True(result);
        Assert.True(resetter.WasWiped);
    }

    private sealed class TestDatabaseResetter : AIAgentHub.Application.Security.IDatabaseResetter
    {
        public bool WasWiped { get; private set; }
        public Task WipeAllDataAsync(CancellationToken cancellationToken = default)
        {
            WasWiped = true;
            return Task.CompletedTask;
        }
    }
}
