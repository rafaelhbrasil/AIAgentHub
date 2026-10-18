using AIAgentHub.Application.Rendering;

namespace AgentHub.UnitTests.Application.Rendering;

public sealed class ExpandedContentRendererTests
{
    [Fact]
    public async Task PdfRenderer_ShouldRenderEmbedObject()
    {
        var renderer = new PdfContentRenderer();
        Assert.True(renderer.CanRender(".pdf", "application/pdf"));
        Assert.False(renderer.CanRender(".txt", "text/plain"));

        var dummyBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var result = await renderer.RenderAsync("document.pdf", dummyBytes);

        Assert.Equal("application/pdf", result.ContentType);
        Assert.True(result.IsBinary);
        Assert.Contains("pdf-preview-object", result.RenderedHtml);
        Assert.StartsWith("data:application/pdf;base64,", result.RawText);
    }

    [Fact]
    public async Task HtmlRenderer_ShouldRenderSandboxIframe()
    {
        var renderer = new HtmlContentRenderer();
        Assert.True(renderer.CanRender(".html", "text/html"));
        Assert.True(renderer.CanRender(".htm", "text/html"));

        var htmlBytes = System.Text.Encoding.UTF8.GetBytes("<h1>Test Document</h1>");
        var result = await renderer.RenderAsync("index.html", htmlBytes);

        Assert.Equal("text/html", result.ContentType);
        Assert.False(result.IsBinary);
        Assert.Contains("html-preview-frame", result.RenderedHtml);
        Assert.Contains("sandbox=\"allow-scripts\"", result.RenderedHtml);
    }

    [Fact]
    public async Task DelimitedRenderer_ShouldRenderCsvAndTsvTables()
    {
        var renderer = new DelimitedContentRenderer();
        Assert.True(renderer.CanRender(".csv", "text/csv"));
        Assert.True(renderer.CanRender(".tsv", "text/tab-separated-values"));

        var csv = "Id,Name,Role\n1,Alice,Admin\n2,Bob,User";
        var csvResult = await renderer.RenderAsync("users.csv", System.Text.Encoding.UTF8.GetBytes(csv));
        Assert.Equal("text/csv", csvResult.ContentType);
        Assert.Contains("<th>Id</th>", csvResult.RenderedHtml);
        Assert.Contains("<td>Alice</td>", csvResult.RenderedHtml);

        var tsv = "Key\tValue\nHost\tlocalhost";
        var tsvResult = await renderer.RenderAsync("config.tsv", System.Text.Encoding.UTF8.GetBytes(tsv));
        Assert.Equal("text/tab-separated-values", tsvResult.ContentType);
        Assert.Contains("<th>Key</th>", tsvResult.RenderedHtml);
        Assert.Contains("<td>localhost</td>", tsvResult.RenderedHtml);
    }

    [Fact]
    public async Task ConfigLogRenderer_ShouldFormatSupportedExtensions()
    {
        var renderer = new ConfigLogContentRenderer();
        Assert.True(renderer.CanRender(".log", null));
        Assert.True(renderer.CanRender(".ini", null));
        Assert.True(renderer.CanRender(".toml", null));
        Assert.True(renderer.CanRender(".env", null));
        Assert.False(renderer.CanRender(".cs", null));

        var log = "[2026-10-05 12:00:00] INFO Server started";
        var result = await renderer.RenderAsync("app.log", System.Text.Encoding.UTF8.GetBytes(log));
        Assert.Equal("text/log", result.ContentType);
        Assert.Contains("config-preview", result.RenderedHtml);
        Assert.Contains("Server started", result.RenderedHtml);
    }
}
