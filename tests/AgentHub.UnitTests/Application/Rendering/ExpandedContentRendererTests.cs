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

        var dummyBytes = "%PDF"u8.ToArray();
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
    public async Task DelimitedRenderer_WithOver200Rows_RendersTruncatedNotice()
    {
        var renderer = new DelimitedContentRenderer();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Header1,Header2");
        for (var i = 1; i <= 210; i++)
        {
            sb.AppendLine($"val{i}_a,val{i}_b");
        }

        var result = await renderer.RenderAsync("large.csv", System.Text.Encoding.UTF8.GetBytes(sb.ToString()));
        Assert.Contains("preview-truncated-notice", result.RenderedHtml);
        Assert.Contains("Showing first 200 rows", result.RenderedHtml);
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

    [Fact]
    public async Task JsonContentRenderer_ValidAndInvalidJson_RendersCleanly()
    {
        var renderer = new JsonContentRenderer();
        Assert.True(renderer.CanRender(".json", null));
        Assert.True(renderer.CanRender(".other", "application/json"));

        var validJson = "{\"name\":\"AgentHub\",\"active\":true}";
        var validResult = await renderer.RenderAsync("app.json", System.Text.Encoding.UTF8.GetBytes(validJson));
        Assert.Equal("application/json", validResult.ContentType);
        Assert.Contains("&quot;AgentHub&quot;", validResult.RenderedHtml);
        Assert.Contains("\"AgentHub\"", validResult.RawText);

        var invalidJson = "{\"name\": unfinished";
        var invalidResult = await renderer.RenderAsync("bad.json", System.Text.Encoding.UTF8.GetBytes(invalidJson));
        Assert.Equal("application/json", invalidResult.ContentType);
        Assert.Contains("unfinished", invalidResult.RenderedHtml);
    }

    [Fact]
    public async Task XmlContentRenderer_ValidAndInvalidXml_RendersCleanly()
    {
        var renderer = new XmlContentRenderer();
        Assert.True(renderer.CanRender(".xml", null));
        Assert.True(renderer.CanRender(".config", null));
        Assert.True(renderer.CanRender(".other", "application/xml"));
        Assert.False(renderer.CanRender(".svg", "image/svg+xml"));

        var validXml = "<root><item>Hello</item></root>";
        var validResult = await renderer.RenderAsync("data.xml", System.Text.Encoding.UTF8.GetBytes(validXml));
        Assert.Equal("application/xml", validResult.ContentType);
        Assert.Contains("Hello", validResult.RenderedHtml);

        var invalidXml = "<root><unclosed>";
        var invalidResult = await renderer.RenderAsync("bad.xml", System.Text.Encoding.UTF8.GetBytes(invalidXml));
        Assert.Equal("application/xml", invalidResult.ContentType);
        Assert.Contains("unclosed", invalidResult.RenderedHtml);
    }

    [Fact]
    public async Task ContentRenderingManager_RegisteredRenderers_And_FallbackRenderer()
    {
        var textRenderer = new TextContentRenderer();
        var jsonRenderer = new JsonContentRenderer();
        var manager = new ContentRenderingManager([textRenderer, jsonRenderer]);

        var renderers = manager.GetRegisteredRenderers();
        Assert.Equal(2, renderers.Count);
        Assert.Equal("JsonRenderer", renderers[0].Name); // Priority 100 > 10

        // Unsupported binary file fallback
        var binaryBytes = new byte[] { 0x00, 0x01, 0x02, 0xFF };
        var result = await manager.RenderFileAsync("firmware.bin", binaryBytes);

        Assert.Equal("FallbackRenderer", result.RendererName);
        Assert.Equal("application/octet-stream", result.ContentType);
        Assert.True(result.IsBinary);
        Assert.Contains("unsupported-file-card", result.RenderedHtml);
        Assert.Contains("Preview is not available for this file type (.bin)", result.RenderedHtml);
    }
}
