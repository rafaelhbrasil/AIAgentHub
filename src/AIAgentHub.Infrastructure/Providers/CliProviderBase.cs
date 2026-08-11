using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Domain.Providers;
using Microsoft.Extensions.Options;

namespace AIAgentHub.Infrastructure.Providers;

public abstract class CliProviderBase : IProvider
{
    private readonly IOptions<CliExecutionOptions>? _options;
    private readonly ConcurrentDictionary<Guid, Process> _activeProcesses = new();

    public CliProviderBase(IOptions<CliExecutionOptions>? options = null)
    {
        _options = options;
    }

    public abstract string Id { get; }
    public abstract string DisplayName { get; }
    public abstract string Description { get; }
    public abstract string ExecutableName { get; }
    public abstract string? InstallInstructions { get; }
    public abstract string? InstallCommand { get; }
    public abstract string? AuthCommand { get; }
    public abstract string? DocumentationUrl { get; }
    public abstract ProviderCapability Capabilities { get; }

    protected CliExecutionOptions GetExecutionOptions() => _options?.Value ?? new CliExecutionOptions();

    public virtual async Task<ProviderInfo> DetectAsync(CancellationToken cancellationToken = default)
    {
        var exePath = FindExecutable(ExecutableName);
        var isInstalled = !string.IsNullOrEmpty(exePath);
        string? version = null;

        if (isInstalled && !string.IsNullOrEmpty(exePath))
        {
            try
            {
                version = await RunVersionCheckAsync(exePath, cancellationToken);
            }
            catch
            {
                version = "1.0.0";
            }
        }

        var models = await GetModelsAsync(cancellationToken);

        return new ProviderInfo
        {
            Id = Id,
            DisplayName = DisplayName,
            Description = Description,
            IsInstalled = isInstalled,
            IsAuthenticated = isInstalled,
            Status = isInstalled ? ProviderStatus.Ready : ProviderStatus.NotInstalled,
            Version = version,
            ExecutablePath = exePath,
            Capabilities = Capabilities,
            SupportedModels = models.ToList(),
            InstallInstructions = InstallInstructions,
            InstallCommand = InstallCommand,
            AuthCommand = AuthCommand,
            DocumentationUrl = DocumentationUrl
        };
    }

    public virtual async Task<ProviderDetectionResult> DetectDetailedAsync(CancellationToken cancellationToken = default)
    {
        var exePath = FindExecutable(ExecutableName);
        
        // Check if installed
        if (string.IsNullOrEmpty(exePath))
        {
            return new ProviderDetectionResult(
                ProviderStatus.NotInstalled,
                $"{DisplayName} is not installed. {InstallInstructions}",
                null,
                TimeSpan.FromHours(1)
            );
        }

        // Try running a test command to check auth/quota
        try
        {
            var testResult = await RunTestCommandAsync(exePath, cancellationToken);
            
            if (testResult.IsSuccess)
            {
                return new ProviderDetectionResult(
                    ProviderStatus.Ready,
                    "Provider is ready to use.",
                    null,
                    TimeSpan.FromMinutes(5)
                );
            }

            // Parse error to determine status
            if (IsQuotaError(testResult.Error))
            {
                var resetTime = ParseQuotaResetTime(testResult.Error ?? "");
                return new ProviderDetectionResult(
                    ProviderStatus.QuotaExceeded,
                    testResult.Error,
                    resetTime,
                    TimeSpan.FromHours(1)
                );
            }

            if (IsAuthError(testResult.Error))
            {
                return new ProviderDetectionResult(
                    ProviderStatus.Unauthenticated,
                    testResult.Error,
                    null,
                    TimeSpan.FromMinutes(30)
                );
            }

            return new ProviderDetectionResult(
                ProviderStatus.Error,
                testResult.Error ?? "Unknown error occurred.",
                null,
                TimeSpan.FromMinutes(10)
            );
        }
        catch (Exception ex)
        {
            return new ProviderDetectionResult(
                ProviderStatus.Error,
                $"Failed to detect provider status: {ex.Message}",
                null,
                TimeSpan.FromMinutes(5)
            );
        }
    }

    protected virtual async Task<TestCommandResult> RunTestCommandAsync(string exePath, CancellationToken cancellationToken)
    {
        // Default test: run version check
        try
        {
            var version = await RunVersionCheckAsync(exePath, cancellationToken);
            return new TestCommandResult(true, null);
        }
        catch (Exception ex)
        {
            return new TestCommandResult(false, ex.Message);
        }
    }

    protected virtual bool IsQuotaError(string? error)
    {
        if (string.IsNullOrEmpty(error)) return false;
        var lower = error.ToLowerInvariant();
        return lower.Contains("quota") || 
               lower.Contains("rate limit") || 
               lower.Contains("too many requests") ||
               lower.Contains("429");
    }

    protected virtual bool IsAuthError(string? error)
    {
        if (string.IsNullOrEmpty(error)) return false;
        var lower = error.ToLowerInvariant();
        return lower.Contains("auth") || 
               lower.Contains("login") || 
               lower.Contains("unauthorized") ||
               lower.Contains("401") ||
               lower.Contains("credential");
    }

    protected virtual DateTimeOffset? ParseQuotaResetTime(string error)
    {
        // Try to parse common reset time formats
        // Example: "Rate limit exceeded. Resets in 2 hours"
        var match = Regex.Match(error, @"resets? in (\d+) (hour|minute|day)s?", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var amount = int.Parse(match.Groups[1].Value);
            var unit = match.Groups[2].Value.ToLowerInvariant();
            var duration = unit switch
            {
                "hour" => TimeSpan.FromHours(amount),
                "minute" => TimeSpan.FromMinutes(amount),
                "day" => TimeSpan.FromDays(amount),
                _ => TimeSpan.FromHours(1)
            };
            return DateTimeOffset.UtcNow.Add(duration);
        }

        // Example: "Try again after 2024-01-01T12:00:00Z"
        match = Regex.Match(error, @"try again after (\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z?)", RegexOptions.IgnoreCase);
        if (match.Success && DateTimeOffset.TryParse(match.Groups[1].Value, out var resetTime))
        {
            return resetTime;
        }

        return null;
    }

    public abstract Task<IReadOnlyList<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default);

    public virtual Task<string?> StartSessionAsync(Guid conversationId, string workspacePath, string? modelId, CancellationToken cancellationToken = default)
    {
        // Default implementation: generate a session ID based on conversation ID
        // Providers can override to use their own session management
        var sessionId = $"agenthub-{conversationId}";
        return Task.FromResult<string?>(sessionId);
    }

    public virtual async Task ExecuteAsync(ProviderExecutionContext context)
    {
        var exePath = FindExecutable(ExecutableName);
        if (string.IsNullOrEmpty(exePath))
        {
            // When CLI is not installed on system, generate an intelligent simulation/fallback response explaining next steps
            await context.OnStreamToken($"[{DisplayName}] Provider executable '{ExecutableName}' was not found in system PATH.\n\n");
            await context.OnStreamToken($"**Prompt received:** {context.Prompt}\n\n");
            await context.OnStreamToken($"To enable full agentic capabilities, please install {DisplayName} using the command below:\n```bash\n{InstallCommand}\n```\n");
            return;
        }

        var execOptions = GetExecutionOptions();

        if (!execOptions.Headless && OperatingSystem.IsWindows())
        {
            try
            {
                var cliCmd = FormatArgumentsForShell(exePath, context);
                var psWindowInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    WorkingDirectory = context.WorkspacePath,
                    Arguments = $"-NoExit -Command \"$Host.UI.RawUI.WindowTitle = 'AI Agent Hub — {DisplayName}'; Write-Host '=== [AI Agent Hub] Active Session: {DisplayName} ===' -ForegroundColor Cyan; {cliCmd}\"",
                    UseShellExecute = true,
                    CreateNoWindow = false
                };
                Process.Start(psWindowInfo);
                await context.OnStreamToken($"[{DisplayName}] External PowerShell session launched on desktop.\n\n");
            }
            catch { }
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = context.WorkspacePath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        ConfigureStartInfo(startInfo, context);

        using var process = new Process { StartInfo = startInfo };
        _activeProcesses[context.ConversationId] = process;
        try
        {
            process.Start();
            if (startInfo.RedirectStandardInput)
            {
                try { process.StandardInput.Close(); } catch { }
            }

            var readOutputTask = Task.Run(async () =>
            {
                var buffer = new char[512];
                int read;
                try
                {
                    while ((read = await process.StandardOutput.ReadAsync(buffer, 0, buffer.Length)) > 0)
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
                    while ((read = await process.StandardError.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        var rawChunk = new string(buffer, 0, read);
                        var cleanChunk = Regex.Replace(rawChunk, @"\x1B\[[^@-~]*[@-~]", "");

                        if (string.IsNullOrWhiteSpace(cleanChunk) ||
                            cleanChunk.TrimStart().StartsWith(">") ||
                            cleanChunk.Contains("build ·"))
                        {
                            continue;
                        }

                        await context.OnStreamToken($"\n[Error]: {cleanChunk}");
                    }
                }
                catch { }
            });

            await process.WaitForExitAsync(context.CancellationToken);
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await Task.WhenAny(Task.WhenAll(readOutputTask, readErrorTask), Task.Delay(1000, cts.Token));
            }
            catch { }
        }
        finally
        {
            _activeProcesses.TryRemove(context.ConversationId, out _);
        }
    }

    public virtual Task<string> LaunchAuthenticationAsync(CancellationToken cancellationToken = default)
    {
        var exePath = FindExecutable(ExecutableName);
        if (string.IsNullOrEmpty(exePath))
            return Task.FromResult($"Provider '{DisplayName}' is not installed.");

        // Start official native terminal authentication process
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoExit -Command \"Write-Host 'Authenticating {DisplayName}...'; & '{exePath}' {AuthCommand}\"",
            UseShellExecute = true
        };

        Process.Start(psi);
        return Task.FromResult($"Authentication window launched for {DisplayName}.");
    }

    public virtual Task AbortAsync(Guid conversationId)
    {
        if (_activeProcesses.TryRemove(conversationId, out var process))
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
                process.Dispose();
            }
            catch
            {
                // Process may have already exited
            }
        }
        return Task.CompletedTask;
    }

    public virtual Task EndSessionAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        AbortAsync(conversationId);
        return Task.CompletedTask;
    }

    protected virtual void ConfigureStartInfo(ProcessStartInfo psi, ProviderExecutionContext context)
    {
        psi.Arguments = $"--prompt \"{context.Prompt.Replace("\"", "\\\"")}\"";
        if (!string.IsNullOrEmpty(context.ModelId))
        {
            psi.Arguments += $" --model {context.ModelId}";
        }
        if (!string.IsNullOrEmpty(context.ProviderSessionId))
        {
            psi.Arguments += $" --session {context.ProviderSessionId}";
        }
    }

    public virtual string FormatArgumentsForShell(string exePath, ProviderExecutionContext context)
    {
        var modelArg = (!string.IsNullOrEmpty(context.ModelId) && !context.ModelId.Equals("Default Model", StringComparison.OrdinalIgnoreCase))
            ? $" --model '{context.ModelId.Replace("'", "''")}'"
            : "";
        var sessionArg = !string.IsNullOrEmpty(context.ProviderSessionId) ? $" --session '{context.ProviderSessionId.Replace("'", "''")}'" : "";
        var escapedPrompt = context.Prompt.Replace("'", "''");
        return $"& '{exePath}' --prompt '{escapedPrompt}'{modelArg}{sessionArg}";
    }

    protected virtual async Task<string> RunVersionCheckAsync(string exePath, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = "--version",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return output.Trim();
    }

    public static string? FindExecutable(string name)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        var extensions = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT").Split(';')
            : new[] { "" };

        var paths = pathEnv.Split(Path.PathSeparator);
        foreach (var p in paths)
        {
            if (string.IsNullOrWhiteSpace(p) || !Directory.Exists(p)) continue;

            foreach (var ext in extensions)
            {
                var candidate = Path.Combine(p, name + ext);
                if (File.Exists(candidate)) return candidate;
            }
        }

        return null;
    }

    protected record TestCommandResult(bool IsSuccess, string? Error);
}
