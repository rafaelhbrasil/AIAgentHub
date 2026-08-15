using System.Collections.Concurrent;
using System.Diagnostics;

using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Infrastructure.Providers;

namespace AIAgentHub.Infrastructure.Executors;

public record ProcessCommandResult(int ExitCode, string Output, string Error);

public interface IProcessExecutor
{
    public Task ExecuteAsync(
        string displayName,
        string executableName,
        string arguments,
        ProviderExecutionContext context,
        IPromptLogger promptLogger,
        CliExecutionOptions options);

    public Task<ProcessCommandResult> RunCommandAsync(
        string executable,
        string arguments,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default,
        string? operationTitle = null);

    public bool AbortProcess(Guid conversationId);
}

public abstract class ProcessExecutorBase : IProcessExecutor
{
    private readonly ConcurrentDictionary<Guid, Process> _activeProcesses = new();

    public abstract Task ExecuteAsync(
        string displayName,
        string executableName,
        string arguments,
        ProviderExecutionContext context,
        IPromptLogger promptLogger,
        CliExecutionOptions options);

    public abstract Task<ProcessCommandResult> RunCommandAsync(
        string executable,
        string arguments,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default,
        string? operationTitle = null);

    protected ProcessRegistrationScope StartProcess(ProcessStartInfo startInfo, Guid conversationId)
    {
        var process = new Process { StartInfo = startInfo };
        _activeProcesses[conversationId] = process;
        _ = process.Start();
        return new ProcessRegistrationScope(_activeProcesses, conversationId, process);
    }

    public bool AbortProcess(Guid conversationId)
    {
        if (_activeProcesses.TryRemove(conversationId, out var process))
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (Exception)
            {
                // Log the exception if needed
            }
            finally
            {
                process.Dispose();
            }
            return true;
        }
        return false;
    }

    protected virtual void EnsureWindowsPlatform(string executorTypeName)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new NotImplementedException($"{executorTypeName} is not supported on non-Windows operating systems yet.");
        }
    }

    protected virtual string ResolveExecutablePath(string executableName) => CliProviderBase.FindExecutable(executableName) ?? executableName;

    protected static void LogPrompt(
        IPromptLogger promptLogger,
        string displayName,
        string? modelId,
        string? commandLine,
        int promptLength)
    {
        promptLogger.LogPromptSent(
            displayName,
            modelId ?? "default",
            commandLine ?? string.Empty,
            promptLength);
    }

    protected static ProcessStartInfo CreateStartInfo(
        string fileName,
        string arguments,
        string workingDirectory,
        bool useShellExecute,
        bool createNoWindow)
    {
        return new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = useShellExecute,
            CreateNoWindow = createNoWindow
        };
    }

    protected sealed class ProcessRegistrationScope(ConcurrentDictionary<Guid, Process> allProcesses, Guid conversationId, Process process) : IDisposable
    {
        private readonly ConcurrentDictionary<Guid, Process> _allProcesses = allProcesses;
        private readonly Guid _conversationId = conversationId;

        public Process Process { get; } = process;

        public StreamWriter StandardInput => Process.StandardInput;
        public StreamReader StandardOutput => Process.StandardOutput;
        public StreamReader StandardError => Process.StandardError;
        public bool HasExited => Process.HasExited;

        public Task WaitForExitAsync(CancellationToken cancellationToken = default)
            => Process.WaitForExitAsync(cancellationToken);

        public void Kill(bool entireProcessTree = false)
            => Process.Kill(entireProcessTree);

        public static implicit operator Process(ProcessRegistrationScope scope)
        {
            return scope.Process;
        }

        public void Dispose()
        {
            _ = _allProcesses.TryRemove(_conversationId, out _);
            Process.Dispose();
        }
    }
}
