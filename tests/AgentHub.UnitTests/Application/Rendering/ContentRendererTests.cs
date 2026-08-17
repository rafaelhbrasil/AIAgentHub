using AIAgentHub.Application.Rendering;

namespace AgentHub.UnitTests.Application.Rendering;

public sealed class ContentRendererTests
{
    [Fact]
    public async Task Renderers_ShouldFormatContentProperly()
    {
        var mdRenderer = new MarkdownContentRenderer();
        var mdResult = await mdRenderer.RenderAsync("file.md", System.Text.Encoding.UTF8.GetBytes("# Header\n```csharp\ncode\n```"));
        Assert.Equal("text/markdown", mdResult.ContentType);
        Assert.Contains("Header", mdResult.RawText);
        Assert.False(mdResult.IsBinary);

        var jsonRenderer = new JsonContentRenderer();
        var jsonResult = await jsonRenderer.RenderAsync("file.json", System.Text.Encoding.UTF8.GetBytes(/*lang=json,strict*/ "{\"a\":1}"));
        Assert.Equal("application/json", jsonResult.ContentType);
        Assert.Contains("a", jsonResult.RenderedHtml);

        var xmlRenderer = new XmlContentRenderer();
        var xmlResult = await xmlRenderer.RenderAsync("file.xml", System.Text.Encoding.UTF8.GetBytes("<root><item>val</item></root>"));
        Assert.Equal("application/xml", xmlResult.ContentType);
        Assert.Contains("root", xmlResult.RenderedHtml);

        var yamlRenderer = new YamlContentRenderer();
        var yamlResult = await yamlRenderer.RenderAsync("file.yaml", System.Text.Encoding.UTF8.GetBytes("key: value"));
        Assert.Equal("application/x-yaml", yamlResult.ContentType);
        Assert.Contains("key", yamlResult.RenderedHtml);

        var textRenderer = new TextContentRenderer();
        var textResult = await textRenderer.RenderAsync("file.txt", System.Text.Encoding.UTF8.GetBytes("plain text"));
        Assert.Equal("text/plain", textResult.ContentType);

        var manager = new ContentRenderingManager(new IContentRenderer[] { mdRenderer, jsonRenderer, xmlRenderer, yamlRenderer, textRenderer });
        var managedResult = await manager.RenderFileAsync("unknown.xyz", System.Text.Encoding.UTF8.GetBytes("fallback text"));
        Assert.Equal("FallbackRenderer", managedResult.RendererName);
    }
}
