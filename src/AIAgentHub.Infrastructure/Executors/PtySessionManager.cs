using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

using AIAgentHub.Application.Realtime;
using AIAgentHub.Infrastructure.Providers;
using AIAgentHub.Infrastructure.Realtime;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

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
        public required Process Process { get; init; }
        public required CancellationTokenSource Cts { get; init; }
        public StringBuilder OutputBuffer { get; } = new(16384);
        public DateTimeOffset StartedAtUtc { get; } = DateTimeOffset.UtcNow;
        public int? ThinkingTokens { get; set; }
        public string? ActiveBackgroundTask { get; set; }
        public string? CurrentTool { get; set; }

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
                if (!Process.HasExited)
                {
                    Process.Kill(entireProcessTree: true);
                }
                Process.Dispose();
            }
            catch { }
        }
    }

    public Task<PtySessionInfo> GetOrCreateSessionAsync(Guid conversationId, string workspacePath, CancellationToken cancellationToken = default)
    {
        if (_sessions.TryGetValue(conversationId, out var existing) && !existing.Process.HasExited)
        {
            return Task.FromResult(new PtySessionInfo(
                existing.ConversationId,
                existing.Executable,
                existing.WorkspacePath,
                true,
                existing.StartedAtUtc,
                existing.ThinkingTokens,
                existing.ActiveBackgroundTask,
                existing.CurrentTool));
        }

        // Clean up dead session if present
        if (existing != null)
        {
            _ = _sessions.TryRemove(conversationId, out _);
            existing.Dispose();
        }

        var shellExe = CliProviderBase.FindExecutable("pwsh") ?? "powershell.exe";

        var startInfo = new ProcessStartInfo
        {
            FileName = shellExe,
            Arguments = "-NoLogo -NoExit -ExecutionPolicy Bypass",
            WorkingDirectory = Directory.Exists(workspacePath) ? workspacePath : Directory.GetCurrentDirectory(),
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        var cts = new CancellationTokenSource();
        var process = new Process { StartInfo = startInfo };

        try
        {
            _ = process.Start();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch interactive shell for conversation {ConversationId}", conversationId);
            throw;
        }

        var session = new ActiveSession
        {
            ConversationId = conversationId,
            WorkspacePath = workspacePath,
            Executable = Path.GetFileName(shellExe),
            Process = process,
            Cts = cts
        };

        _sessions[conversationId] = session;

        // Configure UTF-8 encoding in PowerShell immediately
        try
        {
            process.StandardInput.WriteLine("[Console]::InputEncoding = [System.Text.Encoding]::UTF8; [Console]::OutputEncoding = [System.Text.Encoding]::UTF8; $OutputEncoding = [System.Text.Encoding]::UTF8");
            process.StandardInput.Flush();
        }
        catch { }

        // Initial banner
        var banner = $"\u001b[36m=== AI Agent Hub — Antigravity 2.0 (PTY Console) ===\u001b[0m\r\n\u001b[90mWorkspace: {workspacePath}\u001b[0m\r\n\u001b[90mShell: {Path.GetFileName(shellExe)} | Session: {conversationId}\u001b[0m\r\n\r\n";
        session.AppendOutput(banner);
        _ = BroadcastTerminalChunkAsync(conversationId, banner);

        // Start background stdout and stderr reading
        _ = Task.Run(() => ReadStreamAsync(session, process.StandardOutput, cts.Token), CancellationToken.None);
        _ = Task.Run(() => ReadStreamAsync(session, process.StandardError, cts.Token), CancellationToken.None);

        return Task.FromResult(new PtySessionInfo(
            session.ConversationId,
            session.Executable,
            session.WorkspacePath,
            true,
            session.StartedAtUtc));
    }

    private async Task ReadStreamAsync(ActiveSession session, StreamReader reader, CancellationToken ct)
    {
        var buffer = new char[1024];
        try
        {
            while (!ct.IsCancellationRequested && !session.Process.HasExited)
            {
                var read = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                if (read <= 0) break;

                var chunk = new string(buffer, 0, read);
                session.AppendOutput(chunk);

                // Broadcast raw terminal chunk to SignalR
                await BroadcastTerminalChunkAsync(session.ConversationId, chunk).ConfigureAwait(false);

                // Parse stream chunk for reasoning tokens, tools, and background tasks
                InspectChunkForMetrics(session, chunk);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Stream reading ended for conversation {ConversationId}", session.ConversationId);
        }
    }

    private void InspectChunkForMetrics(ActiveSession session, string chunk)
    {
        var updated = false;

        // 1. Check for thinking tokens (e.g., thinking_tokens: 66, or "thinking tokens")
        var thinkingMatch = Regex.Match(chunk, @"(?:thinking_tokens["":\s]+|thinking[_\s]tokens[:\s]+)(\d+)", RegexOptions.IgnoreCase);
        if (thinkingMatch.Success && int.TryParse(thinkingMatch.Groups[1].Value, out var tokens))
        {
            session.ThinkingTokens = tokens;
            updated = true;
        }

        // 2. Check for background tasks running (e.g., [18:15:02] dotnet run ... running or task(s) · /tasks)
        var bgMatch = Regex.Match(chunk, @"(\[\d{2}:\d{2}:\d{2}\]\s*.+?\s+running|\b\d+\s+task\(s\)\b|\bdotnet run.+running\b)", RegexOptions.IgnoreCase);
        if (bgMatch.Success)
        {
            session.ActiveBackgroundTask = bgMatch.Groups[1].Value.Trim();
            updated = true;
        }

        // 3. Check for tool execution
        var toolMatch = Regex.Match(chunk, @"(?:Executing|Running tool|Tool:)\s*([A-Za-z0-9_]+)", RegexOptions.IgnoreCase);
        if (toolMatch.Success)
        {
            session.CurrentTool = toolMatch.Groups[1].Value;
            updated = true;
        }

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
        if (!_sessions.TryGetValue(conversationId, out var session) || session.Process.HasExited)
        {
            _logger.LogWarning("Cannot send input: no active PTY session for conversation {ConversationId}", conversationId);
            return;
        }

        try
        {
            await session.Process.StandardInput.WriteAsync(input.AsMemory(), cancellationToken);
            await session.Process.StandardInput.FlushAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write input to PTY process for conversation {ConversationId}", conversationId);
        }
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
        if (_sessions.TryGetValue(conversationId, out var session) && !session.Process.HasExited)
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
        return _sessions.TryGetValue(conversationId, out var session) && !session.Process.HasExited;
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
