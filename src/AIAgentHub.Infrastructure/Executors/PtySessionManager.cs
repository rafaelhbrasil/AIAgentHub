using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

using AIAgentHub.Infrastructure.Providers;
using AIAgentHub.Infrastructure.Realtime;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

using Porta.Pty;

namespace AIAgentHub.Infrastructure.Executors;

public sealed class PtySessionManager(
    IHubContext<AgentHubHub> hubContext,
    ILogger<PtySessionManager> logger) : IPtySessionManager, IAsyncDisposable
{
    private readonly IHubContext<AgentHubHub> _hubContext = hubContext;
    private readonly ILogger<PtySessionManager> _logger = logger;
    private readonly ConcurrentDictionary<Guid, ActiveSession> _sessions = new();

    private sealed class ActiveSession : IDisposable
    {
        public required Guid ConversationId { get; init; }
        public required string WorkspacePath { get; init; }
        public required string Executable { get; init; }
        public required IPtyConnection PtyConnection { get; init; }
        public required CancellationTokenSource Cts { get; init; }
        public StringBuilder OutputBuffer { get; } = new(16384);
        public DateTimeOffset StartedAtUtc { get; } = DateTimeOffset.UtcNow;
        public int? ThinkingTokens { get; set; }
        public string? ActiveBackgroundTask { get; set; }
        public string? CurrentTool { get; set; }
        public bool HasExited { get; set; }

        public void AppendOutput(string chunk)
        {
            lock (OutputBuffer)
            {
                if (OutputBuffer.Length + chunk.Length > 65536)
                {
                    _ = OutputBuffer.Remove(0, Math.Min(OutputBuffer.Length, 32768));
                }
                _ = OutputBuffer.Append(chunk);
            }
        }

        public string GetHistory()
        {
            lock (OutputBuffer)
            {
                return OutputBuffer.ToString();
            }
        }

        public void Dispose()
        {
            try
            {
                Cts.Cancel();
                Cts.Dispose();
            }
            catch { }

            try
            {
                if (PtyConnection.Pid > 0)
                {
                    var proc = System.Diagnostics.Process.GetProcessById(PtyConnection.Pid);
                    proc.Kill(entireProcessTree: true);
                }
            }
            catch { }

            try
            {
                if (!HasExited)
                {
                    PtyConnection.Kill();
                }
            }
            catch { }
        }
    }

    public async Task<PtySessionInfo> GetOrCreateSessionAsync(
        Guid conversationId,
        string workspacePath,
        int cols = 120,
        int rows = 30,
        CancellationToken cancellationToken = default)
    {
        if (_sessions.TryGetValue(conversationId, out var existing) && !existing.HasExited)
        {
            return new PtySessionInfo(
                existing.ConversationId,
                existing.Executable,
                existing.WorkspacePath,
                true,
                existing.StartedAtUtc,
                existing.ThinkingTokens,
                existing.ActiveBackgroundTask,
                existing.CurrentTool);
        }

        if (existing != null)
        {
            _ = _sessions.TryRemove(conversationId, out _);
            existing.Dispose();
        }

        string shellExe;
        string[] args;
        if (OperatingSystem.IsWindows())
        {
            shellExe = CliProviderBase.FindExecutable("pwsh") ?? "powershell.exe";
            args = ["-NoLogo"];
        }
        else
        {
            shellExe = Environment.GetEnvironmentVariable("SHELL") ?? "/bin/bash";
            args = ["-l"];
        }

        var env = new Dictionary<string, string>();
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string k && entry.Value is string v)
            {
                env[k] = v;
            }
        }
        env["TERM"] = "xterm-256color";
        env["COLORTERM"] = "truecolor";

        var targetDir = Directory.Exists(workspacePath) ? workspacePath : Directory.GetCurrentDirectory();
        var options = new PtyOptions
        {
            App = shellExe,
            CommandLine = args,
            Cwd = targetDir,
            Cols = cols > 0 ? cols : 120,
            Rows = rows > 0 ? rows : 30,
            Environment = env
        };

        var cts = new CancellationTokenSource();
        IPtyConnection pty;
        try
        {
            pty = await PtyProvider.SpawnAsync(options, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to spawn PTY session for conversation {ConversationId}", conversationId);
            throw;
        }

        var session = new ActiveSession
        {
            ConversationId = conversationId,
            WorkspacePath = targetDir,
            Executable = Path.GetFileName(shellExe),
            PtyConnection = pty,
            Cts = cts
        };

        pty.ProcessExited += (_, e) =>
        {
            session.HasExited = true;
            _logger.LogInformation("PTY process exited for conversation {ConversationId} with exit code {ExitCode}", conversationId, e.ExitCode);
        };

        _sessions[conversationId] = session;

        // Background reader
        _ = Task.Run(() => ReadPtyStreamAsync(session, pty.ReaderStream, cts.Token), CancellationToken.None);

        return new PtySessionInfo(
            session.ConversationId,
            session.Executable,
            session.WorkspacePath,
            true,
            session.StartedAtUtc);
    }

    private async Task ReadPtyStreamAsync(ActiveSession session, Stream readerStream, CancellationToken ct)
    {
        var buffer = new byte[4096];
        var charBuffer = new char[4096];
        // ponytail: UTF-8 decoder preserves incomplete multi-byte sequences across stream reads
        var decoder = Encoding.UTF8.GetDecoder();
        try
        {
            while (!ct.IsCancellationRequested && !session.HasExited)
            {
                var read = await readerStream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false);
                if (read <= 0) break;

                var charsDecoded = decoder.GetChars(buffer, 0, read, charBuffer, 0, flush: false);
                if (charsDecoded <= 0) continue;

                var chunk = new string(charBuffer, 0, charsDecoded);
                session.AppendOutput(chunk);

                _logger.LogInformation("PTY read {Count}b for {ConvId}: {Preview}", read, session.ConversationId, chunk.Length > 80 ? chunk[..80].Replace("\r", "\\r").Replace("\n", "\\n") : chunk.Replace("\r", "\\r").Replace("\n", "\\n"));

                await BroadcastTerminalChunkAsync(session.ConversationId, chunk).ConfigureAwait(false);

                InspectChunkForMetrics(session, chunk);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "PTY stream reading ended for conversation {ConversationId}", session.ConversationId);
        }
    }

    private void InspectChunkForMetrics(ActiveSession session, string chunk)
    {
        var updated = false;

        try
        {
            var thinkingMatch = Regex.Match(chunk, @"(?:thinking_tokens["":\s]+|thinking[_\s]tokens[:\s]+)(\d+)", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(50));
            if (thinkingMatch.Success && int.TryParse(thinkingMatch.Groups[1].Value, out var tokens))
            {
                session.ThinkingTokens = tokens;
                updated = true;
            }

            var bgMatch = Regex.Match(chunk, @"(\[\d{2}:\d{2}:\d{2}\]\s*.+?\s+running|\b\d+\s+task\(s\)\b|\bdotnet run.+running\b)", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(50));
            if (bgMatch.Success)
            {
                session.ActiveBackgroundTask = bgMatch.Groups[1].Value.Trim();
                updated = true;
            }

            var toolMatch = Regex.Match(chunk, @"(?:Executing|Running tool|Tool:)\s*([A-Za-z0-9_]+)", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(50));
            if (toolMatch.Success)
            {
                session.CurrentTool = toolMatch.Groups[1].Value;
                updated = true;
            }
        }
        catch (RegexMatchTimeoutException) { }

        if (updated)
        {
            _ = BroadcastMetaAsync(session);
        }
    }

    private async Task BroadcastTerminalChunkAsync(Guid conversationId, string chunk)
    {
        try
        {
            await _hubContext.Clients.Group($"conv_{conversationId}").SendAsync("terminalOutput", new
            {
                conversationId,
                chunk
            });
        }
        catch { }
    }

    private async Task BroadcastMetaAsync(ActiveSession session)
    {
        try
        {
            await _hubContext.Clients.Group($"conv_{session.ConversationId}").SendAsync("terminalMeta", new
            {
                conversationId = session.ConversationId,
                thinkingTokens = session.ThinkingTokens,
                activeBackgroundTask = session.ActiveBackgroundTask,
                currentTool = session.CurrentTool
            });
        }
        catch { }
    }

    public async Task SendInputAsync(Guid conversationId, string input, CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryGetValue(conversationId, out var session) || session.HasExited)
        {
            _logger.LogWarning("Cannot send input: no active PTY session for conversation {ConversationId}", conversationId);
            return;
        }

        _logger.LogInformation("PTY sendInput for {ConvId}: {Input}", conversationId, input.Replace("\r", "\\r").Replace("\n", "\\n"));

        try
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            await session.PtyConnection.WriterStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await session.PtyConnection.WriterStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write input to PTY for conversation {ConversationId}", conversationId);
        }
    }

    public Task ResizeSessionAsync(Guid conversationId, int cols, int rows, CancellationToken cancellationToken = default)
    {
        if (_sessions.TryGetValue(conversationId, out var session) && !session.HasExited)
        {
            _logger.LogInformation("PTY resize for {ConvId}: {Cols}x{Rows}", conversationId, cols, rows);
            try
            {
                if (cols > 0 && rows > 0)
                {
                    session.PtyConnection.Resize(cols, rows);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to resize PTY session for conversation {ConversationId}", conversationId);
            }
        }
        return Task.CompletedTask;
    }

    public Task TerminateSessionAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        if (_sessions.TryRemove(conversationId, out var session))
        {
            session.Dispose();
            _logger.LogInformation("Terminated PTY session for conversation {ConversationId}", conversationId);
        }
        return Task.CompletedTask;
    }

    public string GetRecentOutput(Guid conversationId)
    {
        return _sessions.TryGetValue(conversationId, out var session)
            ? session.GetHistory()
            : string.Empty;
    }

    public PtySessionInfo? GetSessionInfo(Guid conversationId)
    {
        if (_sessions.TryGetValue(conversationId, out var session) && !session.HasExited)
        {
            return new PtySessionInfo(
                session.ConversationId,
                session.Executable,
                session.WorkspacePath,
                true,
                session.StartedAtUtc,
                session.ThinkingTokens,
                session.ActiveBackgroundTask,
                session.CurrentTool);
        }
        return null;
    }

    public bool HasActiveSession(Guid conversationId)
    {
        return _sessions.TryGetValue(conversationId, out var session) && !session.HasExited;
    }

    public ValueTask DisposeAsync()
    {
        foreach (var session in _sessions.Values)
        {
            session.Dispose();
        }
        _sessions.Clear();
        return ValueTask.CompletedTask;
    }
}
