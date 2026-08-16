using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace AIAgentHub.Application.Rendering;

public sealed class TextContentRenderer : IContentRenderer
{
    public string Name => "TextRenderer";
    public int Priority => 10;

    private static readonly HashSet<string> SupportedExts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".log", ".cs", ".ts", ".js", ".jsx", ".tsx", ".css", ".html",
        ".sql", ".sh", ".ps1", ".bat", ".cmd", ".env", ".gitignore", ".editorconfig",
        ".sln", ".csproj", ".props", ".targets", ".rs", ".go", ".py", ".java", ".cpp", ".c", ".h"
    };

    public bool CanRender(string fileExtension, string? mimeType) => SupportedExts.Contains(fileExtension) || (mimeType?.StartsWith("text/") ?? false);

    public Task<RenderedContentResult> RenderAsync(string filePath, byte[] content, string? mimeType = null, CancellationToken cancellationToken = default)
    {
        var text = Encoding.UTF8.GetString(content);
        var ext = Path.GetExtension(filePath).TrimStart('.');
        var escaped = System.Net.WebUtility.HtmlEncode(text);
        var html = $"<pre class=\"code-preview\"><code class=\"language-{ext}\">{escaped}</code></pre>";

        return Task.FromResult(new RenderedContentResult(filePath, Name, "text/plain", html, text, false, content.Length));
    }
}

public sealed class MarkdownContentRenderer : IContentRenderer
{
    public string Name => "MarkdownRenderer";
    public int Priority => 100;

    public bool CanRender(string fileExtension, string? mimeType)
    {
        return string.Equals(fileExtension, ".md", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileExtension, ".markdown", StringComparison.OrdinalIgnoreCase);
    }

    public Task<RenderedContentResult> RenderAsync(string filePath, byte[] content, string? mimeType = null, CancellationToken cancellationToken = default)
    {
        var mdText = Encoding.UTF8.GetString(content);
        return Task.FromResult(new RenderedContentResult(filePath, Name, "text/markdown", string.Empty, mdText, false, content.Length));
    }
}

public sealed class ImageContentRenderer : IContentRenderer
{
    public string Name => "ImageRenderer";
    public int Priority => 100;

    private static readonly Dictionary<string, string> MimeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".png", "image/png" },
        { ".jpg", "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".gif", "image/gif" },
        { ".webp", "image/webp" },
        { ".svg", "image/svg+xml" },
        { ".bmp", "image/bmp" }
    };

    public bool CanRender(string fileExtension, string? mimeType) => MimeMap.ContainsKey(fileExtension) || (mimeType?.StartsWith("image/") ?? false);

    public Task<RenderedContentResult> RenderAsync(string filePath, byte[] content, string? mimeType = null, CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(filePath);
        var determinedMime = mimeType ?? (MimeMap.TryGetValue(ext, out var m) ? m : "image/png");

        var base64 = Convert.ToBase64String(content);
        var dataUri = $"data:{determinedMime};base64,{base64}";
        var html = $"<div class=\"image-preview-container\"><img src=\"{dataUri}\" alt=\"{Path.GetFileName(filePath)}\" class=\"img-fluid preview-image\" /></div>";

        return Task.FromResult(new RenderedContentResult(filePath, Name, determinedMime, html, dataUri, true, content.Length));
    }
}

public sealed class JsonContentRenderer : IContentRenderer
{
    public string Name => "JsonRenderer";
    public int Priority => 100;

    public bool CanRender(string fileExtension, string? mimeType)
    {
        return string.Equals(fileExtension, ".json", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(mimeType, "application/json", StringComparison.OrdinalIgnoreCase);
    }

    public Task<RenderedContentResult> RenderAsync(string filePath, byte[] content, string? mimeType = null, CancellationToken cancellationToken = default)
    {
        var text = Encoding.UTF8.GetString(content);
        string formatted;
        try
        {
            using var doc = JsonDocument.Parse(text);
            formatted = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            formatted = text;
        }

        var escaped = System.Net.WebUtility.HtmlEncode(formatted);
        var html = $"<pre class=\"code-preview\"><code class=\"language-json\">{escaped}</code></pre>";

        return Task.FromResult(new RenderedContentResult(filePath, Name, "application/json", html, formatted, false, content.Length));
    }
}

public sealed class XmlContentRenderer : IContentRenderer
{
    public string Name => "XmlRenderer";
    public int Priority => 100;

    public bool CanRender(string fileExtension, string? mimeType)
    {
        return string.Equals(fileExtension, ".xml", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileExtension, ".config", StringComparison.OrdinalIgnoreCase) ||
               (!string.Equals(fileExtension, ".svg", StringComparison.OrdinalIgnoreCase) &&
               (mimeType?.Contains("xml") ?? false));
    }

    public Task<RenderedContentResult> RenderAsync(string filePath, byte[] content, string? mimeType = null, CancellationToken cancellationToken = default)
    {
        var text = Encoding.UTF8.GetString(content);
        string formatted;
        try
        {
            var xDoc = XDocument.Parse(text);
            formatted = xDoc.ToString();
        }
        catch
        {
            formatted = text;
        }

        var escaped = System.Net.WebUtility.HtmlEncode(formatted);
        var html = $"<pre class=\"code-preview\"><code class=\"language-xml\">{escaped}</code></pre>";

        return Task.FromResult(new RenderedContentResult(filePath, Name, "application/xml", html, formatted, false, content.Length));
    }
}

public sealed class YamlContentRenderer : IContentRenderer
{
    public string Name => "YamlRenderer";
    public int Priority => 100;

    public bool CanRender(string fileExtension, string? mimeType)
    {
        return string.Equals(fileExtension, ".yaml", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileExtension, ".yml", StringComparison.OrdinalIgnoreCase);
    }

    public Task<RenderedContentResult> RenderAsync(string filePath, byte[] content, string? mimeType = null, CancellationToken cancellationToken = default)
    {
        var text = Encoding.UTF8.GetString(content);
        var escaped = System.Net.WebUtility.HtmlEncode(text);
        var html = $"<pre class=\"code-preview\"><code class=\"language-yaml\">{escaped}</code></pre>";

        return Task.FromResult(new RenderedContentResult(filePath, Name, "application/x-yaml", html, text, false, content.Length));
    }
}

public sealed class ContentRenderingManager(IEnumerable<IContentRenderer> renderers) : IContentRenderingManager
{
    private readonly IEnumerable<IContentRenderer> _renderers = renderers.OrderByDescending(r => r.Priority);

    public IReadOnlyList<IContentRenderer> GetRegisteredRenderers() => _renderers.ToList();

    public async Task<RenderedContentResult> RenderFileAsync(string filePath, byte[] content, string? mimeType = null, CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(filePath);

        foreach (var renderer in _renderers)
        {
            if (renderer.CanRender(ext, mimeType))
            {
                return await renderer.RenderAsync(filePath, content, mimeType, cancellationToken);
            }
        }

        // Fallback for binary / unsupported files
        var fileName = Path.GetFileName(filePath);
        var html = $"<div class=\"unsupported-file-card\"><div class=\"icon-box\">📁</div><h3>{System.Net.WebUtility.HtmlEncode(fileName)}</h3><p>Preview is not available for this file type ({ext}). File size: {content.Length:N0} bytes.</p></div>";

        return new RenderedContentResult(filePath, "FallbackRenderer", "application/octet-stream", html, null, true, content.Length);
    }
}
