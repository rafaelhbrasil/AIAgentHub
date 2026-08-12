using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Infrastructure.Providers;

namespace AIAgentHub.Infrastructure.Executors;

public class HeadedProcessExecutor : ProcessExecutorBase
{
    public override async Task ExecuteAsync(
        string displayName,
        string executableName,
        string arguments,
        ProviderExecutionContext context,
        IPromptLogger promptLogger,
        CliExecutionOptions options)
    {
        EnsureWindowsPlatform(nameof(HeadedProcessExecutor));

        var exePath = ResolveExecutablePath(executableName);
        var tempFolder = Path.Combine(Path.GetTempPath(), "AgentHubLogs");
        Directory.CreateDirectory(tempFolder);
        var logFilePath = Path.Combine(tempFolder, $"stream_{context.ConversationId:N}_{Guid.NewGuid():N}.log");

        try
        {
            var escapedLogFilePath = logFilePath.Replace("'", "''");
            var escapedArguments = arguments.Replace("\"", "\\\"");
            var psArguments = $"-NoExit -Command \"$Host.UI.RawUI.WindowTitle = 'AI Agent Hub — {displayName}'; Write-Host '=== [AI Agent Hub] Active Session: {displayName} ===' -ForegroundColor Cyan; & '{exePath}' {escapedArguments} | Tee-Object -FilePath '{escapedLogFilePath}'\"";

            var psStartInfo = CreateStartInfo(
                fileName: "powershell.exe",
                arguments: psArguments,
                workingDirectory: context.WorkspacePath,
                useShellExecute: true,
                createNoWindow: false);

            LogPrompt(
                promptLogger,
                displayName,
                context.ModelId,
                psStartInfo.Arguments,
                context.Prompt?.Length ?? 0);

            await context.OnStreamToken($"[{displayName}] External PowerShell session launched on desktop.\n\n");

            using var processScope = StartProcess(psStartInfo, context.ConversationId);

            using var tailCts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
            var tailTask = StreamLogFileAsync(logFilePath, context, tailCts.Token);

            try
            {
                var delaySeconds = options.HeadedAutoCloseDelaySeconds;
                if (delaySeconds > 0)
                {
                    using var delayCts = new CancellationTokenSource(TimeSpan.FromSeconds(delaySeconds));
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, delayCts.Token);
                    try
                    {
                        await processScope.WaitForExitAsync(linkedCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        if (!processScope.HasExited && delayCts.IsCancellationRequested && !context.CancellationToken.IsCancellationRequested)
                        {
                            try
                            {
                                processScope.Kill(entireProcessTree: true);
                            }
                            catch { }
                        }
                    }
                }
                else
                {
                    await processScope.WaitForExitAsync(context.CancellationToken);
                }
            }
            finally
            {
                await Task.Delay(200);
                tailCts.Cancel();
                try { await tailTask; } catch { }
            }
        }
        finally
        {
            try
            {
                if (File.Exists(logFilePath))
                {
                    File.Delete(logFilePath);
                }
            }
            catch { }
        }
    }

    public async Task StreamLogFileAsync(string logFilePath, ProviderExecutionContext context, CancellationToken cancellationToken)
    {
        var startWait = DateTime.UtcNow;
        while (!File.Exists(logFilePath) && !cancellationToken.IsCancellationRequested)
        {
            if (DateTime.UtcNow - startWait > TimeSpan.FromSeconds(5))
                break;
            try
            {
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        if (!File.Exists(logFilePath)) return;

        try
        {
            using var fs = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var sr = new StreamReader(fs, Encoding.UTF8);

            var buffer = new char[1024];
            while (!cancellationToken.IsCancellationRequested)
            {
                var read = await sr.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                if (read > 0)
                {
                    var chunk = new string(buffer, 0, read);
                    await context.OnStreamToken(chunk).ConfigureAwait(false);
                }
                else
                {
                    try
                    {
                        await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }

            int readCount;
            while ((readCount = await sr.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
            {
                var chunk = new string(buffer, 0, readCount);
                await context.OnStreamToken(chunk).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        catch { }
    }
}
