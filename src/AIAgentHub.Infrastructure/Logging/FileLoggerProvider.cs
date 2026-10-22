using System.Collections.Concurrent;
using System.Text;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AIAgentHub.Infrastructure.Logging;

public sealed class FileLoggerProvider(string pathPattern) : ILoggerProvider
{
    private readonly string _pathPattern = pathPattern;
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new(StringComparer.OrdinalIgnoreCase);

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name => new FileLogger(name, _pathPattern));

    public void Dispose() => _loggers.Clear();
}

public sealed class FileLogger(string categoryName, string pathPattern) : ILogger
{
    private readonly string _categoryName = categoryName;
    private readonly string _pathPattern = pathPattern;
    private static readonly Lock FileLock = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel) || formatter == null) return;

        var message = formatter(state, exception);
        if (string.IsNullOrEmpty(message) && exception == null) return;

        var sb = new StringBuilder();
        _ = sb.Append($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} UTC] [{logLevel}] [{_categoryName}] {message}");
        if (exception != null)
        {
            _ = sb.AppendLine();
            _ = sb.Append(exception);
        }

        try
        {
            var dateStr = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var fileName = _pathPattern.Replace("{Date}", dateStr, StringComparison.OrdinalIgnoreCase);
            var fullPath = Path.IsPathRooted(fileName)
                ? fileName
                : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), fileName));

            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                _ = Directory.CreateDirectory(dir);
            }

            lock (FileLock)
            {
                File.AppendAllText(fullPath, sb.ToString() + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
            // Logging failures should never crash application
        }
    }
}

public static class FileLoggerExtensions
{
    public static ILoggingBuilder AddFile(this ILoggingBuilder builder, IConfiguration configuration)
    {
        var fileSection = configuration.GetSection("Logging:File");
        var isEnabled = fileSection.GetValue<bool>("Enabled");
        if (!isEnabled)
        {
            return builder;
        }

        var pathPattern = fileSection.GetValue<string>("Path") ?? "logs/app-{Date}.log";
        _ = builder.AddProvider(new FileLoggerProvider(pathPattern));
        return builder;
    }
}
