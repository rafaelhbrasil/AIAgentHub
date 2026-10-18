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

public sealed class PdfContentRenderer : IContentRenderer
{
    public string Name => "PdfRenderer";
    public int Priority => 100;

    public bool CanRender(string fileExtension, string? mimeType)
    {
        return string.Equals(fileExtension, ".pdf", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(mimeType, "application/pdf", StringComparison.OrdinalIgnoreCase);
    }

    public Task<RenderedContentResult> RenderAsync(string filePath, byte[] content, string? mimeType = null, CancellationToken cancellationToken = default)
    {
        var base64 = Convert.ToBase64String(content);
        var dataUri = $"data:application/pdf;base64,{base64}";
        var fileName = Path.GetFileName(filePath);
        var html = $"<div class=\"pdf-preview-container\"><object data=\"{dataUri}\" type=\"application/pdf\" class=\"pdf-preview-object\" style=\"width:100%;height:100%;min-height:550px;border:none;border-radius:6px;\"><div class=\"pdf-fallback-banner\"><p>PDF Preview for <strong>{System.Net.WebUtility.HtmlEncode(fileName)}</strong></p><a href=\"{dataUri}\" download=\"{System.Net.WebUtility.HtmlEncode(fileName)}\" class=\"btn btn-primary compact-btn\">Download PDF</a></div></object></div>";

        return Task.FromResult(new RenderedContentResult(filePath, Name, "application/pdf", html, dataUri, true, content.Length));
    }
}

public sealed class HtmlContentRenderer : IContentRenderer
{
    public string Name => "HtmlRenderer";
    public int Priority => 100;

    public bool CanRender(string fileExtension, string? mimeType)
    {
        return string.Equals(fileExtension, ".html", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileExtension, ".htm", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(mimeType, "text/html", StringComparison.OrdinalIgnoreCase);
    }

    public Task<RenderedContentResult> RenderAsync(string filePath, byte[] content, string? mimeType = null, CancellationToken cancellationToken = default)
    {
        var text = Encoding.UTF8.GetString(content);
        var escaped = System.Net.WebUtility.HtmlEncode(text);
        var html = $"<div class=\"html-preview-wrapper\"><div class=\"html-preview-toolbar\"><span class=\"badge\">HTML Sandbox</span></div><iframe class=\"html-preview-frame\" sandbox=\"allow-scripts\" srcdoc=\"{escaped}\" style=\"width:100%;height:100%;min-height:550px;border:1px solid var(--border-color);border-radius:6px;background:#ffffff;\"></iframe></div>";

        return Task.FromResult(new RenderedContentResult(filePath, Name, "text/html", html, text, false, content.Length));
    }
}

public sealed class DelimitedContentRenderer : IContentRenderer
{
    public string Name => "DelimitedRenderer";
    public int Priority => 100;

    public bool CanRender(string fileExtension, string? mimeType)
    {
        return string.Equals(fileExtension, ".csv", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileExtension, ".tsv", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(mimeType, "text/csv", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(mimeType, "text/tab-separated-values", StringComparison.OrdinalIgnoreCase);
    }

    public Task<RenderedContentResult> RenderAsync(string filePath, byte[] content, string? mimeType = null, CancellationToken cancellationToken = default)
    {
        var text = Encoding.UTF8.GetString(content);
        var isTsv = Path.GetExtension(filePath).Equals(".tsv", StringComparison.OrdinalIgnoreCase);
        var delimiter = isTsv ? '\t' : ',';

        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        sb.Append("<div class=\"table-preview-container\"><table class=\"data-table\">");

        for (var i = 0; i < lines.Length && i < 200; i++)
        {
            var row = lines[i];
            var cells = row.Split(delimiter);
            var isHeader = i == 0;
            var tag = isHeader ? "th" : "td";

            sb.Append("<tr>");
            foreach (var cell in cells)
            {
                var cleanCell = System.Net.WebUtility.HtmlEncode(cell.Trim().Trim('"'));
                sb.Append($"<{tag}>{cleanCell}</{tag}>");
            }
            sb.Append("</tr>");
        }

        sb.Append("</table>");
        if (lines.Length > 200)
        {
            sb.Append($"<p class=\"preview-truncated-notice\">Showing first 200 rows of {lines.Length:N0} total rows.</p>");
        }
        sb.Append("</div>");

        return Task.FromResult(new RenderedContentResult(filePath, Name, isTsv ? "text/tab-separated-values" : "text/csv", sb.ToString(), text, false, content.Length));
    }
}

public sealed class ConfigLogContentRenderer : IContentRenderer
{
    public string Name => "ConfigLogRenderer";
    public int Priority => 90;

    private static readonly HashSet<string> ConfigExts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".log", ".ini", ".toml", ".env"
    };

    public bool CanRender(string fileExtension, string? mimeType) => ConfigExts.Contains(fileExtension);

    public Task<RenderedContentResult> RenderAsync(string filePath, byte[] content, string? mimeType = null, CancellationToken cancellationToken = default)
    {
        var text = Encoding.UTF8.GetString(content);
        var ext = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();
        var escaped = System.Net.WebUtility.HtmlEncode(text);
        var html = $"<pre class=\"code-preview config-preview\"><code class=\"language-{ext}\">{escaped}</code></pre>";

        return Task.FromResult(new RenderedContentResult(filePath, Name, $"text/{ext}", html, text, false, content.Length));
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
