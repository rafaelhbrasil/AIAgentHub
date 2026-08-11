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

    public bool CanRender(string fileExtension, string? mimeType)
    {
        return SupportedExts.Contains(fileExtension) || (mimeType?.StartsWith("text/") ?? false);
    }

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
        // Robust server-side markdown to structured HTML transformation
        var html = RenderMarkdownToHtml(mdText);

        return Task.FromResult(new RenderedContentResult(filePath, Name, "text/markdown", html, mdText, false, content.Length));
    }

    public static string RenderMarkdownToHtml(string md)
    {
        if (string.IsNullOrWhiteSpace(md)) return "<p><em>Empty markdown document</em></p>";

        var lines = md.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var sb = new StringBuilder();
        bool inCodeBlock = false;
        string? codeLang = null;
        var codeSb = new StringBuilder();

        foreach (var rawLine in lines)
        {
            var line = rawLine;

            if (line.TrimStart().StartsWith("```"))
            {
                if (!inCodeBlock)
                {
                    inCodeBlock = true;
                    codeLang = line.TrimStart().Substring(3).Trim();
                    codeSb.Clear();
                }
                else
                {
                    inCodeBlock = false;
                    var escapedCode = System.Net.WebUtility.HtmlEncode(codeSb.ToString());
                    sb.AppendLine($"<pre class=\"code-block\"><code class=\"language-{codeLang}\">{escapedCode}</code></pre>");
                }
                continue;
            }

            if (inCodeBlock)
            {
                codeSb.AppendLine(line);
                continue;
            }

            // Headers
            if (line.StartsWith("# "))
            {
                sb.AppendLine($"<h1 class=\"md-h1\">{FormatInline(line[2..])}</h1>");
            }
            else if (line.StartsWith("## "))
            {
                sb.AppendLine($"<h2 class=\"md-h2\">{FormatInline(line[3..])}</h2>");
            }
            else if (line.StartsWith("### "))
            {
                sb.AppendLine($"<h3 class=\"md-h3\">{FormatInline(line[4..])}</h3>");
            }
            else if (line.StartsWith("#### "))
            {
                sb.AppendLine($"<h4 class=\"md-h4\">{FormatInline(line[5..])}</h4>");
            }
            else if (line.StartsWith("> "))
            {
                sb.AppendLine($"<blockquote class=\"md-quote\">{FormatInline(line[2..])}</blockquote>");
            }
            else if (line.StartsWith("- ") || line.StartsWith("* "))
            {
                sb.AppendLine($"<li class=\"md-list-item\">{FormatInline(line[2..])}</li>");
            }
            else if (string.IsNullOrWhiteSpace(line))
            {
                sb.AppendLine("<div class=\"md-spacer\"></div>");
            }
            else
            {
                sb.AppendLine($"<p class=\"md-p\">{FormatInline(line)}</p>");
            }
        }

        if (inCodeBlock)
        {
            var escapedCode = System.Net.WebUtility.HtmlEncode(codeSb.ToString());
            sb.AppendLine($"<pre class=\"code-block\"><code class=\"language-{codeLang}\">{escapedCode}</code></pre>");
        }

        return $"<div class=\"markdown-rendered\">{sb}</div>";
    }

    private static string FormatInline(string text)
    {
        var escaped = System.Net.WebUtility.HtmlEncode(text);
        // Basic inline bold and inline code
        escaped = System.Text.RegularExpressions.Regex.Replace(escaped, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
        escaped = System.Text.RegularExpressions.Regex.Replace(escaped, @"`(.+?)`", "<code class=\"inline-code\">$1</code>");
        return escaped;
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

    public bool CanRender(string fileExtension, string? mimeType)
    {
        return MimeMap.ContainsKey(fileExtension) || (mimeType?.StartsWith("image/") ?? false);
    }

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
               string.Equals(fileExtension, ".svg", StringComparison.OrdinalIgnoreCase) == false &&
               (mimeType?.Contains("xml") ?? false);
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

public sealed class ContentRenderingManager : IContentRenderingManager
{
    private readonly IEnumerable<IContentRenderer> _renderers;

    public ContentRenderingManager(IEnumerable<IContentRenderer> renderers)
    {
        _renderers = renderers.OrderByDescending(r => r.Priority);
    }

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
