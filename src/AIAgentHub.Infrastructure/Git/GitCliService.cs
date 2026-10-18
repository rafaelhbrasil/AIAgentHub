using System.Diagnostics;
using System.Text.RegularExpressions;
using AIAgentHub.Application.Git;
using AIAgentHub.Infrastructure.Executors;
using AIAgentHub.Infrastructure.Providers;

namespace AIAgentHub.Infrastructure.Git;

public sealed class GitCliService(IProcessExecutor processExecutor) : IGitService
{
    private readonly IProcessExecutor _processExecutor = processExecutor;

    private static string? ResolveGitPath() => CliProviderBase.FindExecutable("git");

    private async Task<ProcessCommandResult> RunGitAsync(string arguments, string? workingDirectory = null, CancellationToken cancellationToken = default, string? title = null)
    {
        var gitPath = ResolveGitPath() ?? "git";
        return await _processExecutor.RunCommandAsync(gitPath, arguments, workingDirectory, cancellationToken, title ?? $"Git {arguments}");
    }

    public async Task<GitStatusDto> GetStatusAsync(string workspacePath, CancellationToken cancellationToken = default)
    {
        var gitPath = ResolveGitPath();
        if (string.IsNullOrEmpty(gitPath))
        {
            return new GitStatusDto(false, false, null, 0, 0, [], [], []);
        }

        if (string.IsNullOrWhiteSpace(workspacePath) || !Directory.Exists(workspacePath))
        {
            return new GitStatusDto(true, false, null, 0, 0, [], [], []);
        }

        // Check if inside git repository
        var checkRepoResult = await RunGitAsync("rev-parse --is-inside-work-tree", workspacePath, cancellationToken);
        if (checkRepoResult.ExitCode != 0 || !checkRepoResult.Output.Trim().Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            return new GitStatusDto(true, false, null, 0, 0, [], [], []);
        }

        // Get branch
        var branchResult = await RunGitAsync("branch --show-current", workspacePath, cancellationToken);
        var currentBranch = branchResult.Output.Trim();
        if (string.IsNullOrEmpty(currentBranch))
        {
            var revResult = await RunGitAsync("rev-parse --short HEAD", workspacePath, cancellationToken);
            currentBranch = revResult.Output.Trim();
        }

        // Run status porcelain
        var statusResult = await RunGitAsync("status --porcelain=v1 -b -uall", workspacePath, cancellationToken);
        var lines = statusResult.Output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimEnd('\r'))
            .Where(l => !string.IsNullOrEmpty(l));

        var ahead = 0;
        var behind = 0;
        var staged = new List<GitFileStatusDto>();
        var modified = new List<GitFileStatusDto>();
        var untracked = new List<string>();

        foreach (var rawLine in lines)
        {
            if (rawLine.StartsWith("##"))
            {
                var aheadMatch = Regex.Match(rawLine, @"ahead\s+(\d+)");
                if (aheadMatch.Success) ahead = int.Parse(aheadMatch.Groups[1].Value);

                var behindMatch = Regex.Match(rawLine, @"behind\s+(\d+)");
                if (behindMatch.Success) behind = int.Parse(behindMatch.Groups[1].Value);

                continue;
            }

            if (rawLine.Length < 3) continue;

            var indexStatus = rawLine[0];
            var workTreeStatus = rawLine[1];
            var filePath = rawLine[2..].Trim().Trim('"');

            if (indexStatus == '?' && workTreeStatus == '?')
            {
                untracked.Add(filePath);
            }
            else
            {
                if (indexStatus != ' ' && indexStatus != '?')
                {
                    staged.Add(new GitFileStatusDto(filePath, MapStatusCode(indexStatus), true));
                }
                if (workTreeStatus != ' ' && workTreeStatus != '?')
                {
                    modified.Add(new GitFileStatusDto(filePath, MapStatusCode(workTreeStatus), false));
                }
            }
        }

        return new GitStatusDto(true, true, currentBranch, ahead, behind, staged, modified, untracked);
    }

    private static string MapStatusCode(char c) => c switch
    {
        'M' => "Modified",
        'A' => "Added",
        'D' => "Deleted",
        'R' => "Renamed",
        'C' => "Copied",
        'U' => "Updated",
        '?' => "Untracked",
        _ => c.ToString()
    };

    public async Task<IReadOnlyList<GitBranchDto>> GetBranchesAsync(string workspacePath, CancellationToken cancellationToken = default)
    {
        var branches = new List<GitBranchDto>();
        var res = await RunGitAsync("branch -a --no-color", workspacePath, cancellationToken);
        if (res.ExitCode != 0) return branches;

        var lines = res.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines)
        {
            if (line.Contains("->")) continue;

            var isCurrent = line.StartsWith('*');
            var cleanName = line.TrimStart('*', ' ').Trim();
            var isRemote = cleanName.StartsWith("remotes/");
            if (isRemote)
            {
                cleanName = cleanName["remotes/".Length..];
            }

            branches.Add(new GitBranchDto(cleanName, isCurrent, isRemote));
        }

        return branches;
    }

    public async Task<GitOperationResult> CreateBranchAsync(string workspacePath, string branchName, bool checkout = true, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(branchName)) return new GitOperationResult(false, null, "Branch name cannot be empty.");
        var cleanBranch = branchName.Trim();
        var args = checkout ? $"checkout -b \"{cleanBranch}\"" : $"branch \"{cleanBranch}\"";
        var res = await RunGitAsync(args, workspacePath, cancellationToken);
        return new GitOperationResult(res.ExitCode == 0, res.Output, res.ExitCode == 0 ? null : (string.IsNullOrWhiteSpace(res.Error) ? res.Output : res.Error));
    }

    public async Task<GitOperationResult> CheckoutBranchAsync(string workspacePath, string branchName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(branchName)) return new GitOperationResult(false, null, "Branch name cannot be empty.");
        var cleanBranch = branchName.Trim();
        var res = await RunGitAsync($"checkout \"{cleanBranch}\"", workspacePath, cancellationToken);
        return new GitOperationResult(res.ExitCode == 0, res.Output, res.ExitCode == 0 ? null : (string.IsNullOrWhiteSpace(res.Error) ? res.Output : res.Error));
    }

    public async Task<GitOperationResult> StageAsync(string workspacePath, IReadOnlyList<string>? files = null, bool stageAll = false, CancellationToken cancellationToken = default)
    {
        string args;
        if (stageAll || files == null || files.Count == 0)
        {
            args = "add -A";
        }
        else
        {
            var escapedFiles = string.Join(" ", files.Select(f => $"\"{f.Replace("\"", "\\\"")}\""));
            args = $"add -- {escapedFiles}";
        }

        var res = await RunGitAsync(args, workspacePath, cancellationToken);
        return new GitOperationResult(res.ExitCode == 0, res.Output, res.ExitCode == 0 ? null : (string.IsNullOrWhiteSpace(res.Error) ? res.Output : res.Error));
    }

    public async Task<GitOperationResult> UnstageAsync(string workspacePath, IReadOnlyList<string>? files = null, bool unstageAll = false, CancellationToken cancellationToken = default)
    {
        var isFileMode = !(unstageAll || files == null || files.Count == 0);
        var escapedFiles = isFileMode
            ? string.Join(" ", files!.Select(f => $"\"{f.Replace("\"", "\\\"")}\""))
            : string.Empty;
        var args = isFileMode ? $"restore --staged -- {escapedFiles}" : "restore --staged .";

        var res = await RunGitAsync(args, workspacePath, cancellationToken);
        if (res.ExitCode != 0 &&
            (res.Error.Contains("could not resolve 'HEAD'", StringComparison.OrdinalIgnoreCase) ||
             res.Error.Contains("Needed a single revision", StringComparison.OrdinalIgnoreCase)))
        {
            // Unborn HEAD (repository with no commits yet): git restore --staged cannot resolve HEAD.
            // Fall back to removing the entries directly from the index.
            var fallback = isFileMode
                ? $"rm --cached -- {escapedFiles}"
                : "rm --cached -r --ignore-unmatch .";
            var fallbackRes = await RunGitAsync(fallback, workspacePath, cancellationToken);
            return new GitOperationResult(fallbackRes.ExitCode == 0, fallbackRes.Output, fallbackRes.ExitCode == 0 ? null : (string.IsNullOrWhiteSpace(fallbackRes.Error) ? fallbackRes.Output : fallbackRes.Error));
        }

        return new GitOperationResult(res.ExitCode == 0, res.Output, res.ExitCode == 0 ? null : (string.IsNullOrWhiteSpace(res.Error) ? res.Output : res.Error));
    }

    public async Task<GitOperationResult> CommitAsync(string workspacePath, string message, bool stageAll = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message)) return new GitOperationResult(false, null, "Commit message cannot be empty.");

        if (stageAll)
        {
            var stageRes = await RunGitAsync("add -A", workspacePath, cancellationToken);
            if (stageRes.ExitCode != 0)
            {
                return new GitOperationResult(false, null, string.IsNullOrWhiteSpace(stageRes.Error) ? stageRes.Output : stageRes.Error);
            }
        }

        var escapedMsg = message.Replace("\"", "\\\"").Replace("\r\n", "\n");
        var res = await RunGitAsync($"commit -m \"{escapedMsg}\"", workspacePath, cancellationToken);
        return new GitOperationResult(res.ExitCode == 0, res.Output, res.ExitCode == 0 ? null : (string.IsNullOrWhiteSpace(res.Error) ? res.Output : res.Error));
    }

    public async Task<GitOperationResult> PushAsync(string workspacePath, string? remote = null, string? branch = null, CancellationToken cancellationToken = default)
    {
        var targetRemote = string.IsNullOrWhiteSpace(remote) ? "origin" : remote.Trim();
        var args = string.IsNullOrWhiteSpace(branch) ? $"push {targetRemote}" : $"push -u {targetRemote} \"{branch.Trim()}\"";
        var res = await RunGitAsync(args, workspacePath, cancellationToken);
        return new GitOperationResult(res.ExitCode == 0, res.Output, res.ExitCode == 0 ? null : (string.IsNullOrWhiteSpace(res.Error) ? res.Output : res.Error));
    }

    public async Task<GitOperationResult> PullAsync(string workspacePath, string? remote = null, string? branch = null, CancellationToken cancellationToken = default)
    {
        var args = "pull";
        if (!string.IsNullOrWhiteSpace(remote))
        {
            args += $" {remote.Trim()}";
            if (!string.IsNullOrWhiteSpace(branch))
            {
                args += $" \"{branch.Trim()}\"";
            }
        }
        var res = await RunGitAsync(args, workspacePath, cancellationToken);
        return new GitOperationResult(res.ExitCode == 0, res.Output, res.ExitCode == 0 ? null : (string.IsNullOrWhiteSpace(res.Error) ? res.Output : res.Error));
    }

    public async Task<GitOperationResult> FetchAsync(string workspacePath, string? remote = null, CancellationToken cancellationToken = default)
    {
        var args = string.IsNullOrWhiteSpace(remote) ? "fetch --all" : $"fetch {remote.Trim()}";
        var res = await RunGitAsync(args, workspacePath, cancellationToken);
        return new GitOperationResult(res.ExitCode == 0, res.Output, res.ExitCode == 0 ? null : (string.IsNullOrWhiteSpace(res.Error) ? res.Output : res.Error));
    }

    public async Task<GitOperationResult> StashAsync(string workspacePath, string action = "save", string? message = null, CancellationToken cancellationToken = default)
    {
        var cleanAction = action.ToLowerInvariant().Trim();
        var args = cleanAction switch
        {
            "pop" => "stash pop",
            "apply" => "stash apply",
            "list" => "stash list",
            _ => string.IsNullOrWhiteSpace(message) ? "stash push -u" : $"stash push -u -m \"{message.Trim().Replace("\"", "\\\"")}\""
        };

        var res = await RunGitAsync(args, workspacePath, cancellationToken);
        return new GitOperationResult(res.ExitCode == 0, res.Output, res.ExitCode == 0 ? null : (string.IsNullOrWhiteSpace(res.Error) ? res.Output : res.Error));
    }

    public async Task<IReadOnlyList<GitCommitDto>> GetHistoryAsync(string workspacePath, int maxCount = 50, CancellationToken cancellationToken = default)
    {
        var commits = new List<GitCommitDto>();
        var safeCount = Math.Clamp(maxCount, 1, 200);
        var format = "%H|||%h|||%an|||%ae|||%aI|||%s";
        var res = await RunGitAsync($"log -n {safeCount} --pretty=format:\"{format}\"", workspacePath, cancellationToken);
        if (res.ExitCode != 0) return commits;

        var lines = res.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            var parts = line.Split("|||", StringSplitOptions.None);
            if (parts.Length >= 6)
            {
                _ = DateTimeOffset.TryParse(parts[4], out var dt);
                commits.Add(new GitCommitDto(parts[0], parts[1], parts[2], parts[3], dt, parts[5]));
            }
        }

        return commits;
    }

    public async Task<GitOperationResult> CloneAsync(string cloneUrl, string targetDirectory, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(cloneUrl)) return new GitOperationResult(false, null, "Clone URL cannot be empty.");
        if (string.IsNullOrWhiteSpace(targetDirectory)) return new GitOperationResult(false, null, "Target directory cannot be empty.");

        var fullTarget = Path.GetFullPath(targetDirectory.Trim());
        var parent = Path.GetDirectoryName(fullTarget);
        if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent))
        {
            _ = Directory.CreateDirectory(parent);
        }

        var res = await RunGitAsync($"clone -- \"{cloneUrl.Trim()}\" \"{fullTarget}\"", parent, cancellationToken);
        return new GitOperationResult(res.ExitCode == 0, res.Output, res.ExitCode == 0 ? null : (string.IsNullOrWhiteSpace(res.Error) ? res.Output : res.Error));
    }
}
