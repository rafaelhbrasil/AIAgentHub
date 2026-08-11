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
    string Name { get; }
    int Priority { get; }
    bool CanRender(string fileExtension, string? mimeType);
    Task<RenderedContentResult> RenderAsync(string filePath, byte[] content, string? mimeType = null, CancellationToken cancellationToken = default);
}

public interface IContentRenderingManager
{
    IReadOnlyList<IContentRenderer> GetRegisteredRenderers();
    Task<RenderedContentResult> RenderFileAsync(string filePath, byte[] content, string? mimeType = null, CancellationToken cancellationToken = default);
}
