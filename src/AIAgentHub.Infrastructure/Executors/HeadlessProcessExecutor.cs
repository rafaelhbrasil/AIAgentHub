using System.Collections.Concurrent;
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
            createNoWindow: true);

        startInfo.RedirectStandardInput = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.StandardOutputEncoding = Encoding.UTF8;
        startInfo.StandardErrorEncoding = Encoding.UTF8;

        LogPrompt(
            promptLogger,
            displayName,
            context.ModelId,
            startInfo.Arguments,
            context.Prompt?.Length ?? 0);

        using var processScope = StartProcess(startInfo, context.ConversationId);
        
        if (startInfo.RedirectStandardInput)
        {
            try { processScope.StandardInput.Close(); } catch { }
        }

        var readOutputTask = Task.Run(async () =>
        {
            var buffer = new char[512];
            int read;
            try
            {
                while ((read = await processScope.StandardOutput.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    var chunk = new string(buffer, 0, read);
                    await context.OnStreamToken(chunk);
                }
            }
            catch { }
        });

        var readErrorTask = Task.Run(async () =>
        {
            var buffer = new char[512];
            int read;
            try
            {
                while ((read = await processScope.StandardError.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    var rawChunk = new string(buffer, 0, read);
                    var cleanChunk = CleanAnsiCodes(rawChunk);

                    if (ShouldFilterErrorChunk(cleanChunk))
                    {
                        continue;
                    }

                    await context.OnStreamToken($"\n[Error]: {cleanChunk}");
                }
            }
            catch { }
        });

        await processScope.WaitForExitAsync(context.CancellationToken);
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await Task.WhenAny(Task.WhenAll(readOutputTask, readErrorTask), Task.Delay(1000, cts.Token));
        }
        catch { }
    }

    protected static string CleanAnsiCodes(string input)
    {
        return Regex.Replace(input, @"\x1B\[[^@-~]*[@-~]", "");
    }

    protected static bool ShouldFilterErrorChunk(string cleanChunk)
    {
        return string.IsNullOrWhiteSpace(cleanChunk) ||
               cleanChunk.TrimStart().StartsWith(">") ||
               cleanChunk.Contains("build ·");
    }
}
