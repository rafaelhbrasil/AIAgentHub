namespace AIAgentHub.Application.Diagnostics;

public sealed record LogFileSummaryDto(
    string FileName,
    string Date,
    long SizeBytes,
    string SizeFormatted,
    DateTimeOffset LastModifiedUtc,
    bool IsCurrentDay);

public sealed record LogTailDto(
    string FileName,
    int TotalLines,
    IReadOnlyList<string> Lines);

public interface ILogFileService
{
    public Task<IReadOnlyList<LogFileSummaryDto>> GetLogFilesAsync(CancellationToken cancellationToken = default);
    public Task<LogTailDto?> GetLogTailAsync(string fileName, int lines = 200, CancellationToken cancellationToken = default);
    public Task<(Stream Stream, string ContentType, string DownloadFileName)?> GetLogDownloadAsync(string fileName, CancellationToken cancellationToken = default);
}
