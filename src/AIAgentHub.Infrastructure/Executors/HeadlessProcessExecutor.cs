using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Infrastructure.Providers;

namespace AIAgentHub.Infrastructure.Executors;

public class HeadlessProcessExecutor : ProcessExecutorBase
{
    public override async Task ExecuteAsync(
        string displayName,
        string executableName,
        string arguments,
        ProviderExecutionContext context,
        IPromptLogger promptLogger,
        CliExecutionOptions options)
    {
        var exePath = ResolveExecutablePath(executableName);
        if (OperatingSystem.IsWindows() && exePath.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
        {
            var shellExe = CliProviderBase.FindExecutable("pwsh") ?? "powershell.exe";
            arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{exePath}\" {arguments}";
            exePath = shellExe;
        }

        var startInfo = CreateStartInfo(
            exePath,
            arguments,
            context.WorkspacePath,
            useShellExecute: false,
            createNoWindow: true,
            windowStyle: ProcessWindowStyle.Hidden);

        ConfigureStreams(startInfo);

        var timeoutMinutes = options.TimeoutMinutes > 0 ? options.TimeoutMinutes : 10;
        var timeoutMs = timeoutMinutes * 60 * 1000;
        ConfigureTimeoutEnvironment(startInfo, timeoutMs);

        LogPrompt(
            promptLogger,
            displayName,
            context.ModelId,
            $"\"{exePath}\" {arguments}",
            context.Prompt?.Length ?? 0);

        using var processScope = StartProcess(startInfo, context.ConversationId);

        if (startInfo.RedirectStandardInput)
        {
            TryCloseQuietly(processScope.StandardInput);
        }

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(timeoutMinutes));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, timeoutCts.Token);

        var readOutputTask = Task.Run(() => ReadOutputAsync(processScope, context, linkedCts.Token), linkedCts.Token);

        var readErrorTask = Task.Run(() => ReadErrorAsync(processScope, context, linkedCts.Token), linkedCts.Token);

        var heartbeatIntervalSeconds = options.HeartbeatIntervalSeconds > 0 ? options.HeartbeatIntervalSeconds : 60;
        using var heartbeatCts = new CancellationTokenSource();
        var heartbeatTask = Task.Run(() => RunHeartbeatAsync(context, heartbeatIntervalSeconds, heartbeatCts.Token), heartbeatCts.Token);

        try
        {
            await processScope.WaitForExitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !context.CancellationToken.IsCancellationRequested)
        {
            TryKillQuietly(processScope);
            throw new TimeoutException($"Provider execution timed out after {timeoutMinutes} minutes.");
        }
        finally
        {
            await heartbeatCts.CancelAsync();
            try { await heartbeatTask; } catch { /* Non-fatal: heartbeat task cancelled */ }
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            _ = await Task.WhenAny(Task.WhenAll(readOutputTask, readErrorTask), Task.Delay(1000, cts.Token));
        }
        catch { /* Non-fatal: process output drain timeout */ }
    }

    private static async Task ReadOutputAsync(ProcessRegistrationScope processScope, ProviderExecutionContext context, CancellationToken cancellationToken)
    {
        await processScope.StandardOutput.StreamChunksAsync(
            async chunk => await context.OnStreamToken(chunk),
            cancellationToken,
            512);
    }

    private static async Task ReadErrorAsync(ProcessRegistrationScope processScope, ProviderExecutionContext context, CancellationToken cancellationToken)
    {
        await processScope.StandardError.StreamChunksAsync(
            async rawChunk =>
            {
                var cleanChunk = CleanAnsiCodes(rawChunk);
                if (!ShouldFilterErrorChunk(cleanChunk))
                {
                    await context.OnStreamToken(cleanChunk);
                }
            },
            cancellationToken,
            512);
    }

    private static async Task RunHeartbeatAsync(ProviderExecutionContext context, int heartbeatIntervalSeconds, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var step = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(heartbeatIntervalSeconds), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (cancellationToken.IsCancellationRequested) break;

            step++;
            var elapsedSecs = (int)sw.Elapsed.TotalSeconds;
            var msg = step switch
            {
                1 => $"Still thinking... ({FormatElapsed(elapsedSecs)} elapsed)",
                2 => $"Still working on code and analysis... ({FormatElapsed(elapsedSecs)} elapsed)",
                3 => $"Thinking a little longer on complex task... ({FormatElapsed(elapsedSecs)} elapsed)",
                _ => $"Still running task... ({FormatElapsed(elapsedSecs)} elapsed)"
            };

            if (context.OnHeartbeat != null)
            {
                try
                {
                    await context.OnHeartbeat(msg, elapsedSecs);
                }
                catch { /* Non-fatal: heartbeat consumer error */ }
            }
        }
    }

    private static void ConfigureStreams(ProcessStartInfo startInfo)
    {
        startInfo.RedirectStandardInput = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.StandardOutputEncoding = Encoding.UTF8;
        startInfo.StandardErrorEncoding = Encoding.UTF8;
    }

    private static void ConfigureTimeoutEnvironment(ProcessStartInfo startInfo, int timeoutMs)
    {
        startInfo.EnvironmentVariables["API_TIMEOUT_MS"] = timeoutMs.ToString();
        startInfo.EnvironmentVariables["REQUEST_TIMEOUT_MS"] = timeoutMs.ToString();
        startInfo.EnvironmentVariables["TIMEOUT"] = (timeoutMs / 1000).ToString();
        startInfo.EnvironmentVariables["CLI_TIMEOUT"] = (timeoutMs / 1000).ToString();
        startInfo.EnvironmentVariables["ANTIGRAVITY_TIMEOUT_MS"] = timeoutMs.ToString();
        startInfo.EnvironmentVariables["ANTHROPIC_TIMEOUT_MS"] = timeoutMs.ToString();
        startInfo.EnvironmentVariables["OPENAI_TIMEOUT_MS"] = timeoutMs.ToString();
    }

    private static string FormatElapsed(int totalSeconds)
    {
        var mins = totalSeconds / 60;
        var secs = totalSeconds % 60;
        return mins > 0 ? $"{mins}m {secs:D2}s" : $"{secs}s";
    }

    public override async Task<ProcessCommandResult> RunCommandAsync(
        string executable,
        string arguments,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default,
        string? operationTitle = null)
    {
        var exePath = ResolveExecutablePath(executable);
        if (OperatingSystem.IsWindows() && exePath.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
        {
            var shellExe = CliProviderBase.FindExecutable("pwsh") ?? "powershell.exe";
            arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{exePath}\" {arguments}";
            exePath = shellExe;
        }

        var startInfo = CreateStartInfo(
            fileName: exePath,
            arguments: arguments,
            workingDirectory: workingDirectory ?? "",
            useShellExecute: false,
            createNoWindow: true,
            windowStyle: ProcessWindowStyle.Hidden);

        startInfo.RedirectStandardInput = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.StandardOutputEncoding = Encoding.UTF8;
        startInfo.StandardErrorEncoding = Encoding.UTF8;

        using var process = new Process { StartInfo = startInfo };
        try
        {
            _ = process.Start();
            TryCloseQuietly(process.StandardInput);

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);
            var output = await outputTask;
            var error = await errorTask;
            return new ProcessCommandResult(process.ExitCode, output, error);
        }
        catch
        {
            TryKillQuietly(process);
            throw;
        }
    }

    protected static string CleanAnsiCodes(string input) => Regex.Replace(input, @"\x1B\[[^@-~]*[@-~]", "", RegexOptions.None, TimeSpan.FromSeconds(1));

    protected static bool ShouldFilterErrorChunk(string cleanChunk)
    {
        return string.IsNullOrWhiteSpace(cleanChunk) ||
               cleanChunk.TrimStart().StartsWith('>') ||
               cleanChunk.Contains("build ·", StringComparison.Ordinal);
    }
}
