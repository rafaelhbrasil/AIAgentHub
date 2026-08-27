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
        EnsureWindowsPlatform(nameof(HeadlessProcessExecutor));

        var exePath = ResolveExecutablePath(executableName);

        var startInfo = CreateStartInfo(
            exePath,
            arguments,
            context.WorkspacePath,
            useShellExecute: false,
            createNoWindow: true,
            windowStyle: ProcessWindowStyle.Hidden);

        startInfo.RedirectStandardInput = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.StandardOutputEncoding = Encoding.UTF8;
        startInfo.StandardErrorEncoding = Encoding.UTF8;

        var timeoutMinutes = options.TimeoutMinutes > 0 ? options.TimeoutMinutes : 10;
        var timeoutMs = timeoutMinutes * 60 * 1000;
        startInfo.EnvironmentVariables["API_TIMEOUT_MS"] = timeoutMs.ToString();
        startInfo.EnvironmentVariables["REQUEST_TIMEOUT_MS"] = timeoutMs.ToString();
        startInfo.EnvironmentVariables["TIMEOUT"] = (timeoutMs / 1000).ToString();

        LogPrompt(
            promptLogger,
            displayName,
            context.ModelId,
            $"\"{exePath}\" {arguments}",
            context.Prompt?.Length ?? 0);

        using var processScope = StartProcess(startInfo, context.ConversationId);

        if (startInfo.RedirectStandardInput)
        {
            try { processScope.StandardInput.Close(); } catch { }
        }

        var readOutputTask = Task.Run(async () =>
        {
            await processScope.StandardOutput.StreamChunksAsync(
                async chunk => await context.OnStreamToken(chunk),
                CancellationToken.None,
                512);
        });

        var readErrorTask = Task.Run(async () =>
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
                CancellationToken.None,
                512);
        });

        var heartbeatIntervalSeconds = options.HeartbeatIntervalSeconds > 0 ? options.HeartbeatIntervalSeconds : 60;
        using var heartbeatCts = new CancellationTokenSource();
        var heartbeatTask = Task.Run(async () =>
        {
            var sw = Stopwatch.StartNew();
            var step = 0;
            while (!heartbeatCts.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(heartbeatIntervalSeconds), heartbeatCts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (heartbeatCts.Token.IsCancellationRequested) break;

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
                    catch { }
                }
            }
        });

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(timeoutMinutes));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, timeoutCts.Token);

        try
        {
            await processScope.WaitForExitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !context.CancellationToken.IsCancellationRequested)
        {
            try { if (!processScope.HasExited) processScope.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException($"Provider execution timed out after {timeoutMinutes} minutes.");
        }
        finally
        {
            heartbeatCts.Cancel();
            try { await heartbeatTask; } catch { }
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            _ = await Task.WhenAny(Task.WhenAll(readOutputTask, readErrorTask), Task.Delay(1000, cts.Token));
        }
        catch { }
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
        EnsureWindowsPlatform(nameof(HeadlessProcessExecutor));
        var exePath = ResolveExecutablePath(executable);

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
            try { process.StandardInput.Close(); } catch { }

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);
            var output = await outputTask;
            var error = await errorTask;
            return new ProcessCommandResult(process.ExitCode, output, error);
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
    }

    protected static string CleanAnsiCodes(string input) => Regex.Replace(input, @"\x1B\[[^@-~]*[@-~]", "");

    protected static bool ShouldFilterErrorChunk(string cleanChunk)
    {
        return string.IsNullOrWhiteSpace(cleanChunk) ||
               cleanChunk.TrimStart().StartsWith(">") ||
               cleanChunk.Contains("build ·");
    }
}
