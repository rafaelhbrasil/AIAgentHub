using System.Diagnostics;
using System.Text.RegularExpressions;

using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Domain.Providers;
using AIAgentHub.Infrastructure.Executors;

using Microsoft.Extensions.Options;

namespace AIAgentHub.Infrastructure.Providers;

public abstract class CliProviderBase(
    IOptions<CliExecutionOptions> options,
    IPromptLogger promptLogger,
    IProcessExecutor processExecutor,
    IOptions<ProvidersOptions>? providersOptions = null) : IProvider
{
    private readonly IOptions<CliExecutionOptions> _options = options;
    private readonly IPromptLogger _promptLogger = promptLogger;
    private readonly IProcessExecutor _processExecutor = processExecutor;
    private readonly IOptions<ProvidersOptions>? _providersOptions = providersOptions;

    public abstract string Id { get; }

    protected ProviderSettingsOptions? GetConfig() => _providersOptions?.Value != null && _providersOptions.Value.TryGetValue(Id, out var config) ? config : null;

    protected virtual string DefaultDisplayName => string.Empty;
    protected virtual string DefaultDescription => string.Empty;
    protected virtual string? DefaultInstallInstructions => null;
    protected virtual string? DefaultAuthCommand => null;

    public virtual string DisplayName => GetConfig()?.DisplayName ?? DefaultDisplayName;
    public virtual string Description => GetConfig()?.Description ?? DefaultDescription;

    // ExecutableName and InstallCommand CANNOT be overridden by configuration for safety
    public abstract string ExecutableName { get; }
    public abstract string? InstallCommand { get; }

    public virtual string? InstallInstructions => GetConfig()?.InstallInstructions ?? DefaultInstallInstructions;
    public virtual string? AuthCommand => GetConfig()?.AuthCommand ?? DefaultAuthCommand;

    // DocumentationUrl has NO fallback in C# code. Returns null if missing or whitespace in appsettings.json.
    public virtual string? DocumentationUrl
    {
        get
        {
            var url = GetConfig()?.DocumentationUrl;
            return string.IsNullOrWhiteSpace(url) ? null : url.Trim();
        }
    }

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
            SupportedModels = [.. models],
            InstallInstructions = InstallInstructions,
            InstallCommand = InstallCommand,
            AuthCommand = AuthCommand,
            DocumentationUrl = DocumentationUrl
        };
    }

    public virtual async Task<ProviderDetectionResult> DetectDetailedAsync(CancellationToken cancellationToken = default)
    {
        var exePath = FindExecutable(ExecutableName);

        if (string.IsNullOrEmpty(exePath))
        {
            var notInstalledResult = new ProviderDetectionResult(
                ProviderStatus.NotInstalled,
                $"{DisplayName} is not installed. {InstallInstructions}",
                null
            );
            _promptLogger.LogProviderStatus(DisplayName, notInstalledResult.Status, notInstalledResult.Message);
            return notInstalledResult;
        }

        try
        {
            var testResult = await RunTestCommandAsync(exePath, cancellationToken);

            ProviderDetectionResult detectionResult;
            if (testResult.IsSuccess)
            {
                detectionResult = new ProviderDetectionResult(
                    ProviderStatus.Ready,
                    "Provider is operational and ready to use.",
                    null
                );
            }
            else if (IsQuotaError(testResult.Error))
            {
                var resetTime = ParseQuotaResetTime(testResult.Error ?? "");
                detectionResult = new ProviderDetectionResult(
                    ProviderStatus.QuotaExceeded,
                    testResult.Error,
                    resetTime
                );
            }
            else
            {
                detectionResult = IsAuthError(testResult.Error)
                    ? new ProviderDetectionResult(
                        ProviderStatus.Unauthenticated,
                        testResult.Error,
                        null
                    )
                    : new ProviderDetectionResult(
                    ProviderStatus.Error,
                    testResult.Error ?? "Unknown error occurred.",
                    null
                );
            }

            _promptLogger.LogProviderStatus(DisplayName, detectionResult.Status, detectionResult.Message);
            return detectionResult;
        }
        catch (Exception ex)
        {
            var errorResult = new ProviderDetectionResult(
                ProviderStatus.Error,
                $"Failed to detect provider status: {ex.Message}",
                null
            );
            _promptLogger.LogProviderStatus(DisplayName, errorResult.Status, errorResult.Message);
            return errorResult;
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
        if (string.IsNullOrEmpty(error))
        {
            return false;
        }

        var lower = error.ToLowerInvariant();
        return lower.Contains("quota") ||
               lower.Contains("rate limit") ||
               lower.Contains("too many requests") ||
               lower.Contains("429");
    }

    protected virtual bool IsAuthError(string? error)
    {
        if (string.IsNullOrEmpty(error))
        {
            return false;
        }

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
        return match.Success && DateTimeOffset.TryParse(match.Groups[1].Value, out var resetTime) ? resetTime : null;
    }

    public virtual Task<IReadOnlyList<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDefaultModelList());

    protected virtual async Task<IReadOnlyList<ModelInfo>> TryFetchDynamicModelsAsync(string arguments, CancellationToken cancellationToken)
    {
        var exePath = FindExecutable(ExecutableName);
        if (!string.IsNullOrEmpty(exePath))
        {
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(15));

                var result = await RunCommandAsync(exePath, arguments, null, timeoutCts.Token, $"{DisplayName} — List Models");
                var output = result.Output;

                var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                var dynamicModels = new List<ModelInfo>();
                var isFirst = true;

                foreach (var rawLine in lines)
                {
                    var cleanLine = rawLine.TrimStart('-', '*', ' ', '\t').TrimEnd();
                    if (string.IsNullOrWhiteSpace(cleanLine) ||
                        cleanLine.StartsWith("Usage", StringComparison.OrdinalIgnoreCase) ||
                        cleanLine.StartsWith("Error", StringComparison.OrdinalIgnoreCase) ||
                        cleanLine.StartsWith("Available models", StringComparison.OrdinalIgnoreCase) ||
                        cleanLine.StartsWith("===", StringComparison.OrdinalIgnoreCase) ||
                        cleanLine.StartsWith("---", StringComparison.OrdinalIgnoreCase) ||
                        cleanLine.Contains("[AI Agent Hub]", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string modelId;
                    string displayName;

                    // 1. Check if line contains a tab separator
                    var tabIndex = cleanLine.IndexOf('\t');
                    if (tabIndex > 0)
                    {
                        modelId = cleanLine[..tabIndex].Trim();
                        displayName = cleanLine[(tabIndex + 1)..].Trim();
                        if (string.IsNullOrEmpty(displayName))
                        {
                            displayName = modelId;
                        }
                    }
                    else
                    {
                        // 2. Check if line contains 2 or more consecutive spaces separating ID and Name
                        var match = Regex.Match(cleanLine, @"^(\S+)\s{2,}(.+)$");
                        if (match.Success)
                        {
                            modelId = match.Groups[1].Value.Trim();
                            displayName = match.Groups[2].Value.Trim();
                        }
                        else
                        {
                            modelId = cleanLine;
                            displayName = cleanLine;
                        }
                    }

                    if (displayName == modelId && modelId.Contains('/'))
                    {
                        var parts = modelId.Split('/');
                        var cleanName = parts.Length > 1 ? parts[1] : modelId;
                        displayName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(cleanName.Replace("-", " ").Replace("_", " "));
                    }

                    dynamicModels.Add(new ModelInfo
                    {
                        Id = modelId,
                        DisplayName = displayName == modelId ? displayName : $"{displayName} ({modelId})",
                        Description = $"{DisplayName} model: {displayName}",
                        ContextWindow = null,
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

    protected virtual IReadOnlyList<ModelInfo> CreateDefaultModelList() => Array.Empty<ModelInfo>();

    public virtual Task<string?> StartSessionAsync(Guid conversationId, string workspacePath, string? modelId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

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
        {
            return Task.FromResult($"Provider '{DisplayName}' is not installed.");
        }

        // Start official native terminal authentication process
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoExit -Command \"Write-Host 'Authenticating {DisplayName}...'; & '{exePath}' {AuthCommand}\"",
            UseShellExecute = true
        };

        _ = Process.Start(psi);
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
        var escapedPrompt = context.Prompt.Replace("\"", "\\\"");
        return $"--prompt \"{escapedPrompt}\"{FormatFlag("--model", context.ModelId, skipDefaultModel: true)}{FormatFlag("--session", context.ProviderSessionId)}";
    }

    public static bool IsDefaultModel(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.Trim().Equals("default", StringComparison.OrdinalIgnoreCase);

    protected static string FormatFlag(string flag, string? value, bool skipDefaultModel = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (skipDefaultModel && IsDefaultModel(value))
        {
            return string.Empty;
        }

        return $" {flag} \"{value.Replace("\"", "\\\"")}\"";
    }

    protected virtual async Task<ProcessCommandResult> RunCommandAsync(
        string executable,
        string arguments,
        string? workingDirectory,
        CancellationToken cancellationToken,
        string? operationTitle = null)
    {
        var result = await _processExecutor.RunCommandAsync(
            executable,
            arguments,
            workingDirectory,
            cancellationToken,
            operationTitle);

        _promptLogger.LogCommandResult(
            DisplayName,
            operationTitle ?? $"{DisplayName} command",
            $"\"{executable}\" {arguments}",
            result.ExitCode,
            result.Output,
            result.Error);

        return result;
    }

    protected virtual async Task<string> RunVersionCheckAsync(string exePath, CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

            var result = await RunCommandAsync(exePath, "--version", null, timeoutCts.Token, $"{DisplayName} — Version Check");
            return result.Output.Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    public static string? FindExecutable(string name)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        var extensions = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT").Split(';')
            : [""];

        var paths = pathEnv.Split(Path.PathSeparator);
        foreach (var p in paths)
        {
            if (string.IsNullOrWhiteSpace(p) || !Directory.Exists(p))
            {
                continue;
            }

            foreach (var ext in extensions)
            {
                var candidate = Path.Combine(p, name + ext);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    protected record TestCommandResult(bool IsSuccess, string? Error);
}
