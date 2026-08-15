namespace AIAgentHub.Application.FileChanges;

public enum DiffLineKind
{
    Unchanged = 0,
    Added = 1,
    Deleted = 2,
    Modified = 3
}

public sealed record DiffLine(int? OldLineNumber, int? NewLineNumber, string Content, DiffLineKind Kind);

public sealed record SideBySideLine(int? LeftLineNumber, string? LeftText, DiffLineKind LeftKind, int? RightLineNumber, string? RightText, DiffLineKind RightKind);

public sealed record DiffResult(
    string RelativePath,
    bool IsBinary,
    bool HasChanges,
    int AdditionsCount,
    int DeletionsCount,
    IReadOnlyList<DiffLine> UnifiedLines,
    IReadOnlyList<SideBySideLine> SideBySideLines,
    string? OldContent = null,
    string? NewContent = null);

public interface IDiffEngine
{
    public DiffResult CalculateTextDiff(string relativePath, string? oldText, string? newText);
    public DiffResult CalculateImageDiff(string relativePath, string? oldDataUri, string? newDataUri);
}
