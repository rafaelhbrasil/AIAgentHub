using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Domain.Providers;
using AIAgentHub.Infrastructure.Executors;
using Microsoft.Extensions.Options;

namespace AIAgentHub.Infrastructure.Providers;

public abstract class CliProviderBase : IProvider
{
    private readonly IOptions<CliExecutionOptions> _options;
    private readonly IPromptLogger _promptLogger;
    private readonly IProcessExecutor _processExecutor;

    public CliProviderBase(
        IOptions<CliExecutionOptions> options,
        IPromptLogger promptLogger,
        IProcessExecutor processExecutor)
    {
        _options = options;
        _promptLogger = promptLogger;
        _processExecutor = processExecutor;
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

    protected CliExecutionOptions GetExecutionOptions() => _options.Value;

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

    public virtual Task<IReadOnlyList<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CreateDefaultModelList());
    }

    protected virtual async Task<IReadOnlyList<ModelInfo>> TryFetchDynamicModelsAsync(string arguments, CancellationToken cancellationToken)
    {
        var exePath = FindExecutable(ExecutableName);
        if (!string.IsNullOrEmpty(exePath))
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
                await process.WaitForExitAsync(cancellationToken);

                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var dynamicModels = new List<ModelInfo>();
                bool isFirst = true;

                foreach (var rawLine in lines)
                {
                    var modelLine = rawLine.Trim();
                    if (string.IsNullOrWhiteSpace(modelLine) ||
                        modelLine.StartsWith("Usage", StringComparison.OrdinalIgnoreCase) ||
                        modelLine.StartsWith("Error", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var parts = modelLine.Split('/');
                    var cleanName = parts.Length > 1 ? parts[1] : modelLine;
                    cleanName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(cleanName.Replace("-", " ").Replace("_", " "));

                    dynamicModels.Add(new ModelInfo
                    {
                        Id = modelLine,
                        DisplayName = $"{cleanName} ({modelLine})",
                        Description = $"{DisplayName} model: {modelLine}",
                        ContextWindow = 0,
                        IsDefault = isFirst
                    });

                    isFirst = false;
                }

                if (dynamicModels.Count > 0)
                {
                    return dynamicModels;
                }
            }
            catch { }
        }

        return CreateDefaultModelList();
    }

    protected virtual IReadOnlyList<ModelInfo> CreateDefaultModelList()
    {
        return new List<ModelInfo>
        {
            new()
            {
                Id = "default",
                DisplayName = "Default Model",
                Description = $"Models could not be detected automatically for {DisplayName}. The default model configured in the provider CLI will be used.",
                ContextWindow = 0,
                IsDefault = true
            }
        };
    }

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
            await context.OnStreamToken($"[{DisplayName}] Provider executable '{ExecutableName}' was not found in system PATH.\n\n");
            await context.OnStreamToken($"**Prompt received:** {context.Prompt}\n\n");
            await context.OnStreamToken($"To enable full agentic capabilities, please install {DisplayName} using the command below:\n```bash\n{InstallCommand}\n```\n");
            return;
        }

        var arguments = BuildArguments(context);
        var execOptions = GetExecutionOptions();

        await _processExecutor.ExecuteAsync(
            DisplayName,
            ExecutableName,
            arguments,
            context,
            _promptLogger,
            execOptions);
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

    public virtual Task AbortAsync(Guid conversationId) => Task.FromResult(_processExecutor.AbortProcess(conversationId));

    public virtual Task EndSessionAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        _ = AbortAsync(conversationId);
        return Task.CompletedTask;
    }

    public virtual string BuildArguments(ProviderExecutionContext context)
    {
        var args = $"--prompt \"{context.Prompt.Replace("\"", "\\\"")}\"";
        if (!string.IsNullOrEmpty(context.ModelId))
        {
            args += $" --model \"{context.ModelId.Replace("\"", "\\\"")}\"";
        }
        if (!string.IsNullOrEmpty(context.ProviderSessionId))
        {
            args += $" --session \"{context.ProviderSessionId.Replace("\"", "\\\"")}\"";
        }
        return args;
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
