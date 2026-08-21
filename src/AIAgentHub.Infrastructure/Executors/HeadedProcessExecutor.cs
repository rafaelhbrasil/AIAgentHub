using System.Diagnostics;
using System.Text;

using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Infrastructure.Providers;

using Microsoft.Extensions.Options;

namespace AIAgentHub.Infrastructure.Executors;

public class HeadedProcessExecutor(IOptions<CliExecutionOptions>? options = null) : ProcessExecutorBase
{
    private readonly IOptions<CliExecutionOptions>? _options = options;

    public override async Task<ProcessCommandResult> RunCommandAsync(
        string executable,
        string arguments,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default,
        string? operationTitle = null)
    {
        EnsureWindowsPlatform(nameof(HeadedProcessExecutor));
        var exePath = ResolveExecutablePath(executable);

        var tempFolder = Path.Combine(Path.GetTempPath(), "AgentHubLogs");
        _ = Directory.CreateDirectory(tempFolder);
        var logFilePath = Path.Combine(tempFolder, $"cmd_{Guid.NewGuid():N}.log");
        var runnerScriptPath = Path.Combine(tempFolder, $"cmd_run_{Guid.NewGuid():N}.ps1");

        using var process = new Process();
        try
        {
            var escapedRunnerScriptPath = runnerScriptPath.Replace("'", "''");
            var escapedLogFilePath = logFilePath.Replace("'", "''");

            var autoCloseDelay = _options?.Value?.HeadedAutoCloseDelaySeconds ?? 0;
            var autoCloseScript = autoCloseDelay >= 0
                ? (autoCloseDelay > 0
                    ? $"\r\nWrite-Host \"`n=== [AI Agent Hub] Command Finished ===\" -ForegroundColor Green; Write-Host 'Window will close in {autoCloseDelay}s...' -ForegroundColor DarkGray; Start-Sleep -Seconds {autoCloseDelay}; [System.Environment]::Exit($LASTEXITCODE)\r\n"
                    : "\r\nWrite-Host \"`n=== [AI Agent Hub] Command Finished ===\" -ForegroundColor Green; [System.Environment]::Exit($LASTEXITCODE)\r\n")
                : "\r\nWrite-Host \"`n=== [AI Agent Hub] Command Finished ===\" -ForegroundColor Green\r\n";

            var escapedExeForPs = exePath.Replace("'", "''");

            var runnerContent = $"[Console]::InputEncoding = [System.Text.Encoding]::UTF8; [Console]::OutputEncoding = [System.Text.Encoding]::UTF8; $OutputEncoding = [System.Text.Encoding]::UTF8; & '{escapedExeForPs}' {arguments} 2>&1 | ForEach-Object {{ Write-Host $_; [System.IO.File]::AppendAllText('{escapedLogFilePath}', \"$_`r`n\", [System.Text.Encoding]::UTF8) }}{autoCloseScript}\r\n";
            File.WriteAllText(runnerScriptPath, runnerContent, Encoding.UTF8);

            var title = operationTitle ?? $"{executable} — {arguments}";
            var psArguments = $"-NoExit -ExecutionPolicy Bypass -Command \"[Console]::InputEncoding = [System.Text.Encoding]::UTF8; [Console]::OutputEncoding = [System.Text.Encoding]::UTF8; $OutputEncoding = [System.Text.Encoding]::UTF8; $Host.UI.RawUI.WindowTitle = 'AI Agent Hub — {title}'; Write-Host '=== [AI Agent Hub] Command: {title} ===' -ForegroundColor Cyan; & '{escapedRunnerScriptPath}'\"";

            var shellExe = CliProviderBase.FindExecutable("pwsh") ?? "powershell.exe";

            process.StartInfo = CreateStartInfo(
                fileName: shellExe,
                arguments: psArguments,
                workingDirectory: workingDirectory ?? "",
                useShellExecute: true,
                createNoWindow: false);

            _ = process.Start();
            await process.WaitForExitAsync(cancellationToken);

            var output = File.Exists(logFilePath) ? await File.ReadAllTextAsync(logFilePath, cancellationToken) : "";
            return new ProcessCommandResult(process.ExitCode, output, "");
        }
        catch
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch { }
            throw;
        }
        finally
        {
            try { if (File.Exists(logFilePath)) { File.Delete(logFilePath); } } catch { }
            try { if (File.Exists(runnerScriptPath)) { File.Delete(runnerScriptPath); } } catch { }
        }
    }

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
        _ = Directory.CreateDirectory(tempFolder);
        var logFilePath = Path.Combine(tempFolder, $"stream_{context.ConversationId:N}_{Guid.NewGuid():N}.log");
        var runnerScriptPath = Path.Combine(tempFolder, $"run_{context.ConversationId:N}_{Guid.NewGuid():N}.ps1");

        try
        {
            var escapedRunnerScriptPath = runnerScriptPath.Replace("'", "''");
            var escapedLogFilePath = logFilePath.Replace("'", "''");

            // Write temporary PowerShell runner script that writes to console and appends immediately to log file
            var autoCloseScript = options.HeadedAutoCloseDelaySeconds >= 0
                ? (options.HeadedAutoCloseDelaySeconds > 0
                    ? $"\r\nWrite-Host \"`n=== [AI Agent Hub] Session Finished ===\" -ForegroundColor Green; Write-Host 'Window will close in {options.HeadedAutoCloseDelaySeconds}s...' -ForegroundColor DarkGray; Start-Sleep -Seconds {options.HeadedAutoCloseDelaySeconds}; [System.Environment]::Exit(0)\r\n"
                    : "\r\nWrite-Host \"`n=== [AI Agent Hub] Session Finished ===\" -ForegroundColor Green; [System.Environment]::Exit(0)\r\n")
                : "\r\nWrite-Host \"`n=== [AI Agent Hub] Session Finished ===\" -ForegroundColor Green\r\n";

            var escapedExeForPs = exePath.Replace("'", "''");

            var runnerContent = $"[Console]::InputEncoding = [System.Text.Encoding]::UTF8; [Console]::OutputEncoding = [System.Text.Encoding]::UTF8; $OutputEncoding = [System.Text.Encoding]::UTF8; & '{escapedExeForPs}' {arguments} 2>&1 | ForEach-Object {{ Write-Host $_; [System.IO.File]::AppendAllText('{escapedLogFilePath}', \"$_`r`n\", [System.Text.Encoding]::UTF8) }}{autoCloseScript}\r\n";
            File.WriteAllText(runnerScriptPath, runnerContent, Encoding.UTF8);

            var psArguments = $"-NoExit -ExecutionPolicy Bypass -Command \"[Console]::InputEncoding = [System.Text.Encoding]::UTF8; [Console]::OutputEncoding = [System.Text.Encoding]::UTF8; $OutputEncoding = [System.Text.Encoding]::UTF8; $Host.UI.RawUI.WindowTitle = 'AI Agent Hub — {displayName}'; Write-Host '=== [AI Agent Hub] Active Session: {displayName} ===' -ForegroundColor Cyan; & '{escapedRunnerScriptPath}'\"";

            var shellExe = CliProviderBase.FindExecutable("pwsh") ?? "powershell.exe";

            var psStartInfo = CreateStartInfo(
                fileName: shellExe,
                arguments: psArguments,
                workingDirectory: context.WorkspacePath,
                useShellExecute: true,
                createNoWindow: false);

            LogPrompt(
                promptLogger,
                displayName,
                context.ModelId,
                $"\"{exePath}\" {arguments}",
                context.Prompt?.Length ?? 0);

            await context.OnStreamToken($"[{displayName}] External PowerShell session launched on desktop.\n\n");

            using var processScope = StartProcess(psStartInfo, context.ConversationId);

            using var tailCts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
            var tailTask = StreamLogFileAsync(logFilePath, context, tailCts.Token);

            try
            {
                try
                {
                    await processScope.WaitForExitAsync(context.CancellationToken);
                }
                catch (OperationCanceledException)
                {
                    if (!processScope.HasExited)
                    {
                        try
                        {
                            processScope.Kill(entireProcessTree: true);
                        }
                        catch { }
                    }
                    throw;
                }
            }
            finally
            {
                await Task.Delay(300);
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

            try
            {
                if (File.Exists(runnerScriptPath))
                {
                    File.Delete(runnerScriptPath);
                }
            }
            catch { }
        }
    }

    public async Task StreamLogFileAsync(string logFilePath, ProviderExecutionContext context, CancellationToken cancellationToken)
    {
        long lastReadPosition = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (File.Exists(logFilePath))
            {
                try
                {
                    using var fs = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    if (fs.Length > lastReadPosition)
                    {
                        _ = fs.Seek(lastReadPosition, SeekOrigin.Begin);
                        var buffer = new byte[fs.Length - lastReadPosition];
                        var bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                        if (bytesRead > 0)
                        {
                            lastReadPosition += bytesRead;
                            var text = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            await context.OnStreamToken(text).ConfigureAwait(false);
                        }
                    }
                }
                catch (IOException) { }
                catch (OperationCanceledException) { break; }
            }

            try
            {
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        // Final read pass after cancellation / process completion
        if (File.Exists(logFilePath))
        {
            try
            {
                using var fs = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                if (fs.Length > lastReadPosition)
                {
                    _ = fs.Seek(lastReadPosition, SeekOrigin.Begin);
                    var buffer = new byte[fs.Length - lastReadPosition];
                    var bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None).ConfigureAwait(false);
                    if (bytesRead > 0)
                    {
                        var text = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        await context.OnStreamToken(text).ConfigureAwait(false);
                    }
                }
            }
            catch (IOException) { }
            catch { }
        }
    }
}
