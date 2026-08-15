namespace AIAgentHub.Application.Rendering;

public sealed record RenderedContentResult(
    string FilePath,
    string RendererName,
    string ContentType,
    string RenderedHtml,
    string? RawText = null,
    bool IsBinary = false,
    long SizeBytes = 0);

public interface IContentRenderer
{
    public string Name { get; }
    public int Priority { get; }
    public bool CanRender(string fileExtension, string? mimeType);
    public Task<RenderedContentResult> RenderAsync(string filePath, byte[] content, string? mimeType = null, CancellationToken cancellationToken = default);
}

public interface IContentRenderingManager
{
    public IReadOnlyList<IContentRenderer> GetRegisteredRenderers();
    public Task<RenderedContentResult> RenderFileAsync(string filePath, byte[] content, string? mimeType = null, CancellationToken cancellationToken = default);
}
