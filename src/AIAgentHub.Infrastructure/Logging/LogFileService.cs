using System.Text;

using AIAgentHub.Application.Diagnostics;

using Microsoft.Extensions.Configuration;

namespace AIAgentHub.Infrastructure.Logging;

public sealed class LogFileService(IConfiguration configuration) : ILogFileService
{
    private readonly IConfiguration _configuration = configuration;

    public string GetLogsDirectory()
    {
        var pathPattern = _configuration["Logging:File:Path"] ?? "logs/app-{Date}.log";
        var dirName = Path.GetDirectoryName(pathPattern);
        var relativeOrAbsolute = string.IsNullOrWhiteSpace(dirName) ? "logs" : dirName;

        return Path.IsPathRooted(relativeOrAbsolute)
            ? Path.GetFullPath(relativeOrAbsolute)
            : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), relativeOrAbsolute));
    }

    public Task<IReadOnlyList<LogFileSummaryDto>> GetLogFilesAsync(CancellationToken cancellationToken = default)
    {
        var logsDir = GetLogsDirectory();
        if (!Directory.Exists(logsDir))
        {
            return Task.FromResult<IReadOnlyList<LogFileSummaryDto>>([]);
        }

        var todayStr = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var dirInfo = new DirectoryInfo(logsDir);
        var files = dirInfo.GetFiles("*.log", SearchOption.TopDirectoryOnly);

        var list = files
            .Select(f =>
            {
                var date = ExtractDateFromFileName(f.Name);
                var isCurrentDay = date.Equals(todayStr, StringComparison.OrdinalIgnoreCase) ||
                                   f.Name.Contains(todayStr, StringComparison.OrdinalIgnoreCase);

                return new LogFileSummaryDto(
                    FileName: f.Name,
                    Date: date,
                    SizeBytes: f.Length,
                    SizeFormatted: FormatBytes(f.Length),
                    LastModifiedUtc: new DateTimeOffset(f.LastWriteTimeUtc),
                    IsCurrentDay: isCurrentDay
                );
            })
            .OrderByDescending(f => f.LastModifiedUtc)
            .ToList();

        return Task.FromResult<IReadOnlyList<LogFileSummaryDto>>(list);
    }

    public async Task<LogTailDto?> GetLogTailAsync(string fileName, int lines = 200, CancellationToken cancellationToken = default)
    {
        var fullPath = ValidateAndResolvePath(fileName);
        if (fullPath == null || !File.Exists(fullPath))
        {
            return null;
        }

        var maxLines = Math.Clamp(lines, 1, 1000);
        var queue = new Queue<string>(maxLines);
        var totalLines = 0;

        using var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fileStream, Encoding.UTF8);

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            totalLines++;
            if (queue.Count >= maxLines)
            {
                _ = queue.Dequeue();
            }
            queue.Enqueue(line);
        }

        return new LogTailDto(Path.GetFileName(fullPath), totalLines, queue.ToList());
    }

    public Task<(Stream Stream, string ContentType, string DownloadFileName)?> GetLogDownloadAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var fullPath = ValidateAndResolvePath(fileName);
        if (fullPath == null || !File.Exists(fullPath))
        {
            return Task.FromResult<(Stream Stream, string ContentType, string DownloadFileName)?>(null);
        }

        var downloadStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var sanitizedName = Path.GetFileName(fullPath);

        return Task.FromResult<(Stream Stream, string ContentType, string DownloadFileName)?>((downloadStream, "text/plain; charset=utf-8", sanitizedName));
    }

    private string? ValidateAndResolvePath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var sanitized = Path.GetFileName(fileName);
        if (!sanitized.Equals(fileName, StringComparison.Ordinal) ||
            !sanitized.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var logsDir = GetLogsDirectory();
        var fullPath = Path.GetFullPath(Path.Combine(logsDir, sanitized));
        var fullLogsDir = Path.GetFullPath(logsDir);

        if (!fullPath.StartsWith(fullLogsDir, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return fullPath;
    }

    private static string ExtractDateFromFileName(string fileName)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        var parts = nameWithoutExt.Split('-');
        if (parts.Length >= 4 &&
            int.TryParse(parts[^3], out var y) && y >= 2000 &&
            int.TryParse(parts[^2], out var m) && m is >= 1 and <= 12 &&
            int.TryParse(parts[^1], out var d) && d is >= 1 and <= 31)
        {
            return $"{y:D4}-{m:D2}-{d:D2}";
        }

        return nameWithoutExt;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] suffixes = ["B", "KB", "MB", "GB"];
        var i = 0;
        decimal d = bytes;
        while (Math.Round(d / 1024, 1) >= 1 && i < suffixes.Length - 1)
        {
            d /= 1024;
            i++;
        }
        return $"{d:n1} {suffixes[i]}";
    }
}
