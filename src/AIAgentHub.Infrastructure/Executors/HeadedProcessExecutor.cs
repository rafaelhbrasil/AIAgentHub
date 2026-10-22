using System.Diagnostics;
using System.Text;

using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Infrastructure.Providers;

using Microsoft.Extensions.Options;

namespace AIAgentHub.Infrastructure.Executors;

public class HeadedProcessExecutor(IOptions<CliExecutionOptions> options) : ProcessExecutorBase
{
    private readonly IOptions<CliExecutionOptions> _options = options;

    public override async Task<ProcessCommandResult> RunCommandAsync(
        string executable,
        string arguments,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default,
        string? operationTitle = null)
    {
        var exePath = ResolveExecutablePath(executable);
        var isWindows = OperatingSystem.IsWindows();
        var scriptExt = isWindows ? ".ps1" : ".sh";

        var tempFolder = Path.Combine(Path.GetTempPath(), "AgentHubLogs");
        _ = Directory.CreateDirectory(tempFolder);
        var logFilePath = Path.Combine(tempFolder, $"cmd_{Guid.NewGuid():N}.log");
        var runnerScriptPath = Path.Combine(tempFolder, $"cmd_run_{Guid.NewGuid():N}{scriptExt}");

        using var process = new Process();
        try
        {
            var autoCloseScript = BuildAutoCloseScript(autoCloseDelay: 0, isWindows, isSession: false);

            var runnerContent = isWindows
                ? BuildWindowsRunnerScript(exePath, arguments, logFilePath, autoCloseScript)
                : BuildLinuxRunnerScript(exePath, arguments, logFilePath, autoCloseScript).Replace("\r\n", "\n");
            await File.WriteAllTextAsync(runnerScriptPath, runnerContent, Encoding.UTF8, cancellationToken);
            if (!isWindows)
            {
                try { File.SetUnixFileMode(runnerScriptPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); } catch { /* Non-fatal */ }
            }

            var title = operationTitle ?? $"{executable} — {arguments}";
            process.StartInfo = isWindows
                ? CreateWindowsStartInfo(runnerScriptPath, workingDirectory ?? "", title)
                : CreateStartInfo(
                    fileName: "/bin/bash",
                    arguments: $"\"{runnerScriptPath}\"",
                    workingDirectory: workingDirectory ?? "",
                    useShellExecute: false,
                    createNoWindow: true,
                    windowStyle: ProcessWindowStyle.Hidden);

            _ = process.Start();
            await process.WaitForExitAsync(cancellationToken);

            var output = File.Exists(logFilePath) ? await File.ReadAllTextAsync(logFilePath, cancellationToken) : "";
            return new ProcessCommandResult(process.ExitCode, output, "");
        }
        catch
        {
            TryKillQuietly(process);
            throw;
        }
        finally
        {
            TryDeleteQuietly(logFilePath);
            TryDeleteQuietly(runnerScriptPath);
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
        var exePath = ResolveExecutablePath(executableName);
        var isWindows = OperatingSystem.IsWindows();
        var scriptExt = isWindows ? ".ps1" : ".sh";

        var tempFolder = Path.Combine(Path.GetTempPath(), "AgentHubLogs");
        _ = Directory.CreateDirectory(tempFolder);
        var logFilePath = Path.Combine(tempFolder, $"stream_{context.ConversationId:N}_{Guid.NewGuid():N}.log");
        var runnerScriptPath = Path.Combine(tempFolder, $"run_{context.ConversationId:N}_{Guid.NewGuid():N}{scriptExt}");
        var doneFilePath = Path.Combine(tempFolder, $"done_{context.ConversationId:N}_{Guid.NewGuid():N}.flag");

        try
        {
            var autoCloseScript = BuildAutoCloseScript(options.HeadedAutoCloseDelaySeconds, isWindows, isSession: true);

            var runnerContent = isWindows
                ? BuildWindowsRunnerScript(exePath, arguments, logFilePath, autoCloseScript)
                : BuildLinuxRunnerScript(exePath, arguments, logFilePath, autoCloseScript, doneFilePath).Replace("\r\n", "\n");
            await File.WriteAllTextAsync(runnerScriptPath, runnerContent, Encoding.UTF8, context.CancellationToken);
            if (!isWindows)
            {
                try { File.SetUnixFileMode(runnerScriptPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); } catch { /* Non-fatal */ }
            }

            var startInfo = isWindows
                ? CreateWindowsStartInfo(runnerScriptPath, context.WorkspacePath, displayName)
                : CreateLinuxStartInfo(runnerScriptPath, context.WorkspacePath, displayName);

            LogPrompt(
                promptLogger,
                displayName,
                context.ModelId,
                $"\"{exePath}\" {arguments}",
                context.Prompt?.Length ?? 0);

            var platformSession = isWindows ? "PowerShell" : "terminal";
            await context.OnStreamToken($"[{displayName}] External {platformSession} session launched on desktop.\n\n");

            using var processScope = StartProcess(startInfo, context.ConversationId);

            using var tailCts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
            var tailTask = StreamLogFileAsync(logFilePath, context, tailCts.Token);

            try
            {
                try
                {
                    await processScope.WaitForExitAsync(context.CancellationToken);

                    if (!isWindows && !File.Exists(doneFilePath))
                    {
                        while (!File.Exists(doneFilePath) && !context.CancellationToken.IsCancellationRequested)
                        {
                            await Task.Delay(100, context.CancellationToken);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    TryKillQuietly(processScope);
                    throw;
                }
            }
            finally
            {
                await Task.Delay(300, CancellationToken.None);
                await tailCts.CancelAsync();
                try { await tailTask; } catch { /* Non-fatal: background tail streaming completed */ }
            }
        }
        finally
        {
            TryDeleteQuietly(logFilePath);
            TryDeleteQuietly(runnerScriptPath);
            TryDeleteQuietly(doneFilePath);
        }
    }

    public async Task StreamLogFileAsync(string logFilePath, ProviderExecutionContext context, CancellationToken cancellationToken)
    {
        long lastReadPosition = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            lastReadPosition = await ReadNewContentAsync(logFilePath, lastReadPosition, context, cancellationToken);

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
            catch (IOException) { /* Non-fatal: log file access contention during final read */ }
            catch { /* Non-fatal: log file was already cleaned up */ }
        }
    }

    private static ProcessStartInfo CreateWindowsStartInfo(string runnerScriptPath, string workingDirectory, string title)
    {
        var escapedRunnerScriptPath = runnerScriptPath.Replace("'", "''");
        var shellExe = CliProviderBase.FindExecutable("pwsh") ?? "powershell.exe";
        var psArguments = $"-NoExit -ExecutionPolicy Bypass -Command \"[Console]::InputEncoding = [System.Text.Encoding]::UTF8; [Console]::OutputEncoding = [System.Text.Encoding]::UTF8; $OutputEncoding = [System.Text.Encoding]::UTF8; $Host.UI.RawUI.WindowTitle = 'AI Agent Hub — {title}'; Write-Host '=== [AI Agent Hub] Active Session: {title} ===' -ForegroundColor Cyan; & '{escapedRunnerScriptPath}'\"";

        return CreateStartInfo(
            fileName: shellExe,
            arguments: psArguments,
            workingDirectory: workingDirectory,
            useShellExecute: true,
            createNoWindow: false,
            windowStyle: ProcessWindowStyle.Normal);
    }

    private static ProcessStartInfo CreateLinuxStartInfo(string runnerScriptPath, string workingDirectory, string title)
    {
        var isWsl = !OperatingSystem.IsWindows() && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WSL_DISTRO_NAME"));
        if (isWsl)
        {
            var distro = Environment.GetEnvironmentVariable("WSL_DISTRO_NAME");
            var distroArg = string.IsNullOrEmpty(distro) ? "" : $"-d {distro} ";
            var cmd = CliProviderBase.FindExecutable("cmd.exe");
            if (!string.IsNullOrEmpty(cmd))
            {
                return CreateStartInfo(
                    fileName: cmd,
                    arguments: $"/c start \"AI Agent Hub — {title}\" wsl.exe {distroArg}-e /bin/bash \"{runnerScriptPath}\"",
                    workingDirectory: workingDirectory,
                    useShellExecute: false,
                    createNoWindow: false,
                    windowStyle: ProcessWindowStyle.Normal);
            }
        }

        var term = CliProviderBase.FindExecutable("x-terminal-emulator")
            ?? CliProviderBase.FindExecutable("gnome-terminal")
            ?? CliProviderBase.FindExecutable("konsole")
            ?? CliProviderBase.FindExecutable("xfce4-terminal")
            ?? CliProviderBase.FindExecutable("xterm");

        if (!string.IsNullOrEmpty(term))
        {
            var termName = Path.GetFileName(term).ToLowerInvariant();
            var termArgs = termName switch
            {
                "gnome-terminal" => $"--title=\"AI Agent Hub — {title}\" -- /bin/bash \"{runnerScriptPath}\"",
                _ => $"-T \"AI Agent Hub — {title}\" -e /bin/bash \"{runnerScriptPath}\""
            };

            return CreateStartInfo(
                fileName: term,
                arguments: termArgs,
                workingDirectory: workingDirectory,
                useShellExecute: false,
                createNoWindow: false,
                windowStyle: ProcessWindowStyle.Normal);
        }

        return CreateStartInfo(
            fileName: "/bin/bash",
            arguments: $"\"{runnerScriptPath}\"",
            workingDirectory: workingDirectory,
            useShellExecute: false,
            createNoWindow: false,
            windowStyle: ProcessWindowStyle.Normal);
    }

    private static string BuildAutoCloseScript(int autoCloseDelay, bool isWindows, bool isSession)
    {
        var banner = isSession ? "Session Finished" : "Command Finished";
        if (isWindows)
        {
            if (autoCloseDelay > 0)
            {
                return $"\r\nWrite-Host \"`n=== [AI Agent Hub] {banner} ===\" -ForegroundColor Green; Write-Host 'Window will close in {autoCloseDelay}s...' -ForegroundColor DarkGray; Start-Sleep -Seconds {autoCloseDelay}; [System.Environment]::Exit($exitCode)\r\n";
            }
            if (autoCloseDelay == 0)
            {
                return $"\r\nWrite-Host \"`n=== [AI Agent Hub] {banner} ===\" -ForegroundColor Green; [System.Environment]::Exit($exitCode)\r\n";
            }
            return $"\r\nWrite-Host \"`n=== [AI Agent Hub] {banner} ===\" -ForegroundColor Green\r\n";
        }

        if (autoCloseDelay > 0)
        {
            return $"\necho -e \"\\n=== [AI Agent Hub] {banner} ===\"\necho \"Window will close in {autoCloseDelay}s...\"\nsleep {autoCloseDelay}\nexit $EXIT_CODE\n";
        }
        return $"\necho -e \"\\n=== [AI Agent Hub] {banner} ===\"\nexit $EXIT_CODE\n";
    }

    private static string BuildWindowsRunnerScript(string exePath, string arguments, string logFilePath, string autoCloseScript)
    {
        var escapedExeForPs = exePath.Replace("'", "''");
        var escapedLogFilePath = logFilePath.Replace("'", "''");
        var escapedArgsForPs = arguments.Replace("'", "''");

        return $$"""
            [Console]::InputEncoding = [System.Text.Encoding]::UTF8; [Console]::OutputEncoding = [System.Text.Encoding]::UTF8; $OutputEncoding = [System.Text.Encoding]::UTF8;
            Remove-Item env:ASPNETCORE_URLS -ErrorAction SilentlyContinue; Remove-Item env:ASPNETCORE_HTTPS_PORTS -ErrorAction SilentlyContinue; Remove-Item env:DOTNET_LAUNCH_PROFILE -ErrorAction SilentlyContinue;
            $psi = New-Object System.Diagnostics.ProcessStartInfo
            $psi.FileName = 'cmd.exe'
            $psi.Arguments = '/c ""' + '{{escapedExeForPs}}' + '" ' + '{{escapedArgsForPs}}' + ' > "' + '{{escapedLogFilePath}}' + '" 2>&1"'
            $psi.UseShellExecute = $false
            $psi.CreateNoWindow = $true
            $p = [System.Diagnostics.Process]::Start($psi)

            $attempts = 0
            while (-not (Test-Path '{{escapedLogFilePath}}') -and $attempts -lt 50) {
                [System.Threading.Thread]::Sleep(20)
                $attempts++
            }

            if (Test-Path '{{escapedLogFilePath}}') {
                $fs = [System.IO.File]::Open('{{escapedLogFilePath}}', [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
                $sr = New-Object System.IO.StreamReader($fs, [System.Text.Encoding]::UTF8)

                while (-not $p.HasExited) {
                    while (($line = $sr.ReadLine()) -ne $null) {
                        [System.Console]::WriteLine($line)
                    }
                    [System.Threading.Thread]::Sleep(50)
                }

                $p.WaitForExit()

                while (($line = $sr.ReadLine()) -ne $null) {
                    [System.Console]::WriteLine($line)
                }
                $sr.Dispose()
                $fs.Dispose()
            } else {
                $p.WaitForExit()
            }
            $exitCode = $p.ExitCode
            {{autoCloseScript}}
            """;
    }

    private static string BuildLinuxRunnerScript(string exePath, string arguments, string logFilePath, string autoCloseScript, string? doneFilePath = null)
    {
        var trapDone = string.IsNullOrEmpty(doneFilePath)
            ? ""
            : $"trap 'EXIT_CODE=$?; echo \"$EXIT_CODE\" > \"{doneFilePath}\"' EXIT\n";

        return $$"""
            #!/usr/bin/env bash
            LOG_FILE="{{logFilePath}}"
            touch "$LOG_FILE"
            {{trapDone}}
            unset ASPNETCORE_URLS
            unset ASPNETCORE_HTTPS_PORTS
            unset DOTNET_LAUNCH_PROFILE
            # Launch command in background and redirect output to log file
            "{{exePath}}" {{arguments}} > "$LOG_FILE" 2>&1 &
            CLI_PID=$!

            # Tail log file bound to CLI process lifetime starting from line 1
            tail -n +1 --pid="$CLI_PID" -f "$LOG_FILE"
            wait "$CLI_PID"
            EXIT_CODE=$?
            {{autoCloseScript}}
            exit $EXIT_CODE
            """;
    }

    private static async Task<long> ReadNewContentAsync(string logFilePath, long lastReadPosition, ProviderExecutionContext context, CancellationToken cancellationToken)
    {
        if (!File.Exists(logFilePath))
        {
            return lastReadPosition;
        }

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
        catch (IOException) { /* Non-fatal: log file locked by writer */ }
        catch (OperationCanceledException) { /* Non-fatal: log tail reading cancelled */ }

        return lastReadPosition;
    }
}

