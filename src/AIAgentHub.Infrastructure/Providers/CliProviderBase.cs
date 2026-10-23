using System.Diagnostics;
using System.Globalization;
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

    protected static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    public abstract string Id { get; }

    protected ProviderSettingsOptions? GetConfig() => _providersOptions?.Value != null && _providersOptions.Value.TryGetValue(Id, out var config) ? config : null;

    protected virtual string DefaultDisplayName => string.Empty;
    protected virtual string DefaultDescription => string.Empty;
    protected virtual string? DefaultInstallInstructions => null;
    protected virtual string? DefaultAuthCommand => null;
    protected virtual string? DefaultDocumentationUrl => null;

    public virtual string DisplayName => GetConfig()?.DisplayName ?? DefaultDisplayName;
    public virtual string Description => GetConfig()?.Description ?? DefaultDescription;

    // ExecutableName and InstallCommand CANNOT be overridden by configuration for safety
    public abstract string ExecutableName { get; }
    public abstract string? InstallCommand { get; }

    public virtual string? InstallInstructions => GetConfig()?.InstallInstructions ?? DefaultInstallInstructions;
    public virtual string? AuthCommand => GetConfig()?.AuthCommand ?? DefaultAuthCommand;

    public virtual string? DocumentationUrl
    {
        get
        {
            var url = GetConfig()?.DocumentationUrl ?? DefaultDocumentationUrl;
            return string.IsNullOrWhiteSpace(url) ? null : url.Trim();
        }
    }

    public abstract ProviderCapabilities Capabilities { get; }

    protected CliExecutionOptions GetExecutionOptions() => _options.Value;

    public const string DetachedExecutionInstruction =
        "[Instructions: When running any web application, dev server, dotnet project, or long-running service, always launch it as a detached background process using PowerShell (e.g. Start-Process -FilePath 'dotnet' -ArgumentList 'run' -WindowStyle Hidden; always launch via dotnet run to respect launchSettings.json ports) and return immediately without running sleep, wait loops, or post-startup curl/ping checks, so the command exits immediately without blocking.]";

    public IReadOnlyList<string> GetDetachedExecutionTriggerWords()
    {
        var globalWords = GetExecutionOptions().DetachedExecutionTriggerWords;
        var providerWords = GetConfig()?.DetachedExecutionTriggerWords;

        var merged = new List<string>();
        if (globalWords != null)
        {
            merged.AddRange(globalWords);
        }

        if (providerWords != null)
        {
            merged.AddRange(providerWords);
        }

        return merged
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool ShouldInjectDetachedExecutionInstruction(string prompt, IEnumerable<string> triggerWords)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return false;
        }

        if (prompt.Contains("detached background process", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var words = triggerWords
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Select(w => Regex.Escape(w.Trim()))
            .ToList();

        if (words.Count == 0)
        {
            return false;
        }

        var pattern = $@"\b(?:{string.Join("|", words)})\b";
        return Regex.IsMatch(prompt, pattern, RegexOptions.IgnoreCase, RegexTimeout);
    }

    public virtual bool IsInstalledFastCheck() => !string.IsNullOrEmpty(FindExecutable(ExecutableName));

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
            _ = await RunVersionCheckAsync(exePath, cancellationToken);
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

        return error.Contains("quota", StringComparison.OrdinalIgnoreCase) ||
               error.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
               error.Contains("too many requests", StringComparison.OrdinalIgnoreCase) ||
               error.Contains("429", StringComparison.Ordinal);
    }

    protected virtual bool IsAuthError(string? error)
    {
        if (string.IsNullOrEmpty(error))
        {
            return false;
        }

        return error.Contains("auth", StringComparison.OrdinalIgnoreCase) ||
               error.Contains("login", StringComparison.OrdinalIgnoreCase) ||
               error.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
               error.Contains("401", StringComparison.Ordinal) ||
               error.Contains("credential", StringComparison.OrdinalIgnoreCase);
    }

    protected virtual DateTimeOffset? ParseQuotaResetTime(string error)
    {
        // Try to parse common reset time formats
        // Example: "Rate limit exceeded. Resets in 2 hours"
        var match = Regex.Match(error, @"resets? in (\d+) (hour|minute|day)s?", RegexOptions.IgnoreCase, RegexTimeout);
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
        match = Regex.Match(error, @"try again after (\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z?)", RegexOptions.IgnoreCase, RegexTimeout);
        return match.Success && DateTimeOffset.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var resetTime) ? resetTime : null;
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
                    var model = ParseDynamicModelLine(cleanLine, DisplayName, isFirst);
                    if (model == null)
                    {
                        continue;
                    }

                    dynamicModels.Add(model);
                    isFirst = false;
                }

                if (dynamicModels.Count > 0)
                {
                    return dynamicModels;
                }
            }
            catch { /* Non-fatal: fall back to default predefined models */ }
        }

        return CreateDefaultModelList();
    }

    private static ModelInfo? ParseDynamicModelLine(string cleanLine, string providerName, bool isFirst)
    {
        if (string.IsNullOrWhiteSpace(cleanLine) ||
            cleanLine.StartsWith("Usage", StringComparison.OrdinalIgnoreCase) ||
            cleanLine.StartsWith("Error", StringComparison.OrdinalIgnoreCase) ||
            cleanLine.StartsWith("Available models", StringComparison.OrdinalIgnoreCase) ||
            cleanLine.StartsWith("===", StringComparison.OrdinalIgnoreCase) ||
            cleanLine.StartsWith("---", StringComparison.OrdinalIgnoreCase) ||
            cleanLine.Contains("[AI Agent Hub]", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var (modelId, displayName) = SplitModelLine(cleanLine);

        if (displayName == modelId && modelId.Contains('/'))
        {
            var parts = modelId.Split('/');
            var cleanName = parts.Length > 1 ? parts[1] : modelId;
            displayName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(cleanName.Replace("-", " ").Replace("_", " "));
        }

        return new ModelInfo
        {
            Id = modelId,
            DisplayName = displayName == modelId ? displayName : $"{displayName} ({modelId})",
            Description = $"{providerName} model: {displayName}",
            ContextWindow = null,
            IsDefault = isFirst
        };
    }

    private static (string ModelId, string DisplayName) SplitModelLine(string cleanLine)
    {
        var tabIndex = cleanLine.IndexOf('\t');
        if (tabIndex > 0)
        {
            var modelId = cleanLine[..tabIndex].Trim();
            var displayName = cleanLine[(tabIndex + 1)..].Trim();
            return (modelId, string.IsNullOrEmpty(displayName) ? modelId : displayName);
        }

        var match = Regex.Match(cleanLine, @"^(\S+)\s{2,}(.+)$", RegexOptions.None, RegexTimeout);
        return match.Success
            ? (match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim())
            : (cleanLine, cleanLine);
    }

    protected virtual IReadOnlyList<ModelInfo> CreateDefaultModelList() =>
    [
        new()
        {
            Id = "default",
            DisplayName = "Default",
            Description = "Default model. The model will not be enforced, and whatever was set or used last directly in the provider CLI will remain active without being overridden.",
            ContextWindow = null,
            IsDefault = true,
            IsDisplayed = true
        }
    ];

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
            FileName = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"),
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

        return skipDefaultModel && IsDefaultModel(value) ? string.Empty : $" {flag} \"{value.Replace("\"", "\\\"")}\"";
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
        var isWsl = !OperatingSystem.IsWindows() && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WSL_DISTRO_NAME"));
        var extensions = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT").Split(';')
            : (isWsl ? ["", ".exe", ".EXE", ".cmd", ".bat"] : [""]);

        var searchDirectories = GetSearchDirectories();

        foreach (var p in searchDirectories)
        {
            if (string.IsNullOrWhiteSpace(p) || !Directory.Exists(p))
            {
                continue;
            }

            var candidate = FindInDirectory(p, name, extensions);
            if (candidate != null)
            {
                return candidate;
            }
        }

        return null;
    }

    private static HashSet<string> GetSearchDirectories()
    {
        var searchDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Process PATH
        var processPath = Environment.GetEnvironmentVariable("PATH") ?? "";
        AddPaths(searchDirectories, processPath);

        if (OperatingSystem.IsWindows())
        {
            // 2. User & Machine PATH from Windows Registry (handles cases where Process PATH did not inherit user environment)
            try
            {
                var userPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
                AddPaths(searchDirectories, userPath);

                var machinePath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? "";
                AddPaths(searchDirectories, machinePath);
            }
            catch
            {
                // Fallback if registry access is restricted
            }
        }

        return searchDirectories;
    }

    private static void AddPaths(HashSet<string> directories, string pathValue)
    {
        foreach (var p in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            _ = directories.Add(p);
        }
    }

    private static string? FindInDirectory(string directory, string name, string[] extensions)
    {
        foreach (var ext in extensions)
        {
            var candidate = Path.Combine(directory, name + ext);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    protected record TestCommandResult(bool IsSuccess, string? Error);
}
