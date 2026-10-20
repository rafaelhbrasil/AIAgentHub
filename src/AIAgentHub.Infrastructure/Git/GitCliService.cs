using System.Globalization;
using System.Text.RegularExpressions;
using AIAgentHub.Application.Git;
using AIAgentHub.Infrastructure.Executors;
using AIAgentHub.Infrastructure.Providers;

namespace AIAgentHub.Infrastructure.Git;

public sealed class GitCliService(IProcessExecutor processExecutor) : IGitService
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(500);
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

        var (ahead, behind, staged, modified, untracked) = ParsePorcelainStatus(lines);
        return new GitStatusDto(true, true, currentBranch, ahead, behind, staged, modified, untracked);
    }

    private static (int Ahead, int Behind, List<GitFileStatusDto> Staged, List<GitFileStatusDto> Modified, List<string> Untracked) ParsePorcelainStatus(IEnumerable<string> lines)
    {
        var ahead = 0;
        var behind = 0;
        List<GitFileStatusDto> staged = [];
        List<GitFileStatusDto> modified = [];
        List<string> untracked = [];

        foreach (var rawLine in lines)
        {
            if (rawLine.StartsWith("##", StringComparison.Ordinal))
            {
                var aheadMatch = Regex.Match(rawLine, @"ahead\s+(\d+)", RegexOptions.None, RegexTimeout);
                if (aheadMatch.Success) ahead = int.Parse(aheadMatch.Groups[1].Value, CultureInfo.InvariantCulture);

                var behindMatch = Regex.Match(rawLine, @"behind\s+(\d+)", RegexOptions.None, RegexTimeout);
                if (behindMatch.Success) behind = int.Parse(behindMatch.Groups[1].Value, CultureInfo.InvariantCulture);

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
                if (indexStatus is not ' ' and not '?')
                {
                    staged.Add(new GitFileStatusDto(filePath, MapStatusCode(indexStatus), true));
                }
                if (workTreeStatus is not ' ' and not '?')
                {
                    modified.Add(new GitFileStatusDto(filePath, MapStatusCode(workTreeStatus), false));
                }
            }
        }

        return (ahead, behind, staged, modified, untracked);
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
        List<GitBranchDto> branches = [];
        var res = await RunGitAsync("branch -a --no-color", workspacePath, cancellationToken);
        if (res.ExitCode != 0) return branches;

        var lines = res.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines)
        {
            if (line.Contains("->", StringComparison.Ordinal)) continue;

            var isCurrent = line.StartsWith('*');
            var cleanName = line.TrimStart('*', ' ').Trim();
            var isRemote = cleanName.StartsWith("remotes/", StringComparison.Ordinal);
            if (isRemote)
            {
                cleanName = cleanName["remotes/".Length..];
            }

            branches.Add(new GitBranchDto(cleanName, isCurrent, isRemote));
        }

        return branches;
    }

    private static GitOperationResult ToOperationResult(ProcessCommandResult res)
    {
        if (res.ExitCode == 0)
        {
            return new GitOperationResult(true, res.Output, null);
        }

        var error = string.IsNullOrWhiteSpace(res.Error) ? res.Output : res.Error;
        return new GitOperationResult(false, res.Output, error);
    }

    public async Task<GitOperationResult> CreateBranchAsync(string workspacePath, string branchName, bool checkout = true, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(branchName)) return new GitOperationResult(false, null, "Branch name cannot be empty.");
        var cleanBranch = branchName.Trim();
        var args = checkout ? $"checkout -b \"{cleanBranch}\"" : $"branch \"{cleanBranch}\"";
        var res = await RunGitAsync(args, workspacePath, cancellationToken);
        return ToOperationResult(res);
    }

    public async Task<GitOperationResult> CheckoutBranchAsync(string workspacePath, string branchName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(branchName)) return new GitOperationResult(false, null, "Branch name cannot be empty.");
        var cleanBranch = branchName.Trim();
        var res = await RunGitAsync($"checkout \"{cleanBranch}\"", workspacePath, cancellationToken);
        return ToOperationResult(res);
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
        return ToOperationResult(res);
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
            return ToOperationResult(fallbackRes);
        }

        return ToOperationResult(res);
    }

    public async Task<GitOperationResult> CommitAsync(string workspacePath, string message, bool stageAll = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message)) return new GitOperationResult(false, null, "Commit message cannot be empty.");

        if (stageAll)
        {
            var stageRes = await RunGitAsync("add -A", workspacePath, cancellationToken);
            if (stageRes.ExitCode != 0)
            {
                return ToOperationResult(stageRes);
            }
        }

        var escapedMsg = message.Replace("\"", "\\\"").Replace("\r\n", "\n");
        var res = await RunGitAsync($"commit -m \"{escapedMsg}\"", workspacePath, cancellationToken);
        return ToOperationResult(res);
    }

    public async Task<GitOperationResult> PushAsync(string workspacePath, string? remote = null, string? branch = null, CancellationToken cancellationToken = default)
    {
        var targetRemote = string.IsNullOrWhiteSpace(remote) ? "origin" : remote.Trim();
        var args = string.IsNullOrWhiteSpace(branch) ? $"push {targetRemote}" : $"push -u {targetRemote} \"{branch.Trim()}\"";
        var res = await RunGitAsync(args, workspacePath, cancellationToken);
        return ToOperationResult(res);
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
        return ToOperationResult(res);
    }

    public async Task<GitOperationResult> FetchAsync(string workspacePath, string? remote = null, CancellationToken cancellationToken = default)
    {
        var args = string.IsNullOrWhiteSpace(remote) ? "fetch --all" : $"fetch {remote.Trim()}";
        var res = await RunGitAsync(args, workspacePath, cancellationToken);
        return ToOperationResult(res);
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
        return ToOperationResult(res);
    }

    public async Task<IReadOnlyList<GitCommitDto>> GetHistoryAsync(string workspacePath, int maxCount = 50, CancellationToken cancellationToken = default)
    {
        List<GitCommitDto> commits = [];
        var safeCount = Math.Clamp(maxCount, 1, 200);
        var format = "%H|||%h|||%an|||%ae|||%aI|||%s|||%p|||%d";
        var res = await RunGitAsync($"log -n {safeCount} --pretty=format:\"{format}\"", workspacePath, cancellationToken);
        if (res.ExitCode != 0) return commits;

        var lines = res.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            var parts = line.Split("|||", StringSplitOptions.None);
            if (parts.Length >= 6)
            {
                _ = DateTimeOffset.TryParse(parts[4], CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt);
                var parents = parts.Length >= 7 && !string.IsNullOrWhiteSpace(parts[6])
                    ? parts[6].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    : [];
                var refs = parts.Length >= 8 && !string.IsNullOrWhiteSpace(parts[7])
                    ? parts[7].Trim()
                    : null;
                commits.Add(new GitCommitDto(parts[0], parts[1], parts[2], parts[3], dt, parts[5], parents, refs));
            }
        }

        return commits;
    }

    public async Task<GitCommitDetailDto?> GetCommitDetailsAsync(string workspacePath, string commitHash, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspacePath) || string.IsNullOrWhiteSpace(commitHash))
        {
            return null;
        }

        var cleanHash = commitHash.Trim();
        if (!Regex.IsMatch(cleanHash, "^[a-fA-F0-9]{4,64}$", RegexOptions.None, RegexTimeout))
        {
            return null;
        }

        // 1. Get commit metadata & full body
        var format = "%H|||%h|||%an|||%ae|||%aI|||%B|||%p|||%d";
        var metaRes = await RunGitAsync($"show -s --format=\"{format}\" {cleanHash}", workspacePath, cancellationToken);
        if (metaRes.ExitCode != 0 || string.IsNullOrWhiteSpace(metaRes.Output))
        {
            return null;
        }

        var parts = metaRes.Output.Split("|||", StringSplitOptions.None);
        if (parts.Length < 6)
        {
            return null;
        }

        var hash = parts[0].Trim();
        var shortHash = parts[1].Trim();
        var author = parts[2].Trim();
        var email = parts[3].Trim();
        _ = DateTimeOffset.TryParse(parts[4].Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var date);
        var message = parts[5].Trim();
        var parents = parts.Length >= 7 && !string.IsNullOrWhiteSpace(parts[6])
            ? parts[6].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            : [];
        var refs = parts.Length >= 8 && !string.IsNullOrWhiteSpace(parts[7])
            ? parts[7].Trim()
            : null;

        // 2. Get changed files with status & stats
        var numstatRes = await RunGitAsync($"show --numstat --format=\"\" {cleanHash}", workspacePath, cancellationToken);
        var nameStatusRes = await RunGitAsync($"show --name-status --format=\"\" {cleanHash}", workspacePath, cancellationToken);

        Dictionary<string, (int Additions, int Deletions, string Status)> fileStats = new(StringComparer.OrdinalIgnoreCase);

        if (numstatRes.ExitCode == 0 && !string.IsNullOrWhiteSpace(numstatRes.Output))
        {
            foreach (var line in numstatRes.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var partsStat = line.TrimEnd('\r').Split('\t');
                if (partsStat.Length >= 3)
                {
                    _ = int.TryParse(partsStat[0], out var add);
                    _ = int.TryParse(partsStat[1], out var del);
                    var path = partsStat[2].Trim();
                    fileStats[path] = (add, del, "Modified");
                }
            }
        }

        if (nameStatusRes.ExitCode == 0 && !string.IsNullOrWhiteSpace(nameStatusRes.Output))
        {
            foreach (var line in nameStatusRes.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var partsStatus = line.TrimEnd('\r').Split('\t');
                if (partsStatus.Length >= 2)
                {
                    var rawStatusCode = partsStatus[0].Trim();
                    var path = partsStatus[^1].Trim();
                    var status = MapStatusCodeToName(rawStatusCode);

                    if (fileStats.TryGetValue(path, out var stat))
                    {
                        fileStats[path] = (stat.Additions, stat.Deletions, status);
                    }
                    else
                    {
                        fileStats[path] = (0, 0, status);
                    }
                }
            }
        }

        var files = fileStats
            .Select(kvp => new GitCommitFileChangeDto(kvp.Key, kvp.Value.Status, kvp.Value.Additions, kvp.Value.Deletions))
            .OrderBy(f => f.Path)
            .ToList();

        return new GitCommitDetailDto(hash, shortHash, author, email, date, message, parents, refs, files);
    }

    private static string MapStatusCodeToName(string rawStatusCode)
    {
        if (rawStatusCode.StartsWith("A", StringComparison.OrdinalIgnoreCase)) return "Added";
        if (rawStatusCode.StartsWith("D", StringComparison.OrdinalIgnoreCase)) return "Deleted";
        return rawStatusCode.StartsWith("R", StringComparison.OrdinalIgnoreCase) ? "Renamed" : "Modified";
    }

    public async Task<GitCommitDiffDto?> GetCommitDiffAsync(string workspacePath, string commitHash, string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspacePath) || string.IsNullOrWhiteSpace(commitHash) || string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        var cleanHash = commitHash.Trim();
        var isWorkingTree = cleanHash.Equals("working-tree", StringComparison.OrdinalIgnoreCase);
        if (!isWorkingTree && !Regex.IsMatch(cleanHash, "^[a-fA-F0-9]{4,64}$", RegexOptions.None, RegexTimeout))
        {
            return null;
        }

        // Prevent path traversal
        var normalizedPath = filePath.Trim().Replace('\\', '/').TrimStart('/');
        if (normalizedPath.Contains("..", StringComparison.Ordinal))
        {
            return null;
        }

        string? origContent = null;
        string? modContent = null;

        if (isWorkingTree)
        {
            var origRes = await RunGitAsync($"show \"HEAD:{normalizedPath}\"", workspacePath, cancellationToken);
            if (origRes.ExitCode == 0)
            {
                origContent = origRes.Output;
            }

            var fullPath = Path.GetFullPath(Path.Combine(workspacePath, normalizedPath));
            if (!fullPath.StartsWith(Path.GetFullPath(workspacePath), StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (File.Exists(fullPath))
            {
                modContent = await File.ReadAllTextAsync(fullPath, cancellationToken);
            }

            if (origContent == null && modContent == null)
            {
                return null;
            }
        }
        else
        {
            var origRes = await RunGitAsync($"show \"{cleanHash}~1:{normalizedPath}\"", workspacePath, cancellationToken);
            var modRes = await RunGitAsync($"show \"{cleanHash}:{normalizedPath}\"", workspacePath, cancellationToken);

            if (origRes.ExitCode != 0 && modRes.ExitCode != 0)
            {
                return null;
            }

            origContent = origRes.ExitCode == 0 ? origRes.Output : null;
            modContent = modRes.ExitCode == 0 ? modRes.Output : null;
        }

        var isBinary = (origContent != null && origContent.Contains('\0', StringComparison.Ordinal))
                    || (modContent != null && modContent.Contains('\0', StringComparison.Ordinal));

        return new GitCommitDiffDto(
            normalizedPath,
            isBinary ? null : origContent,
            isBinary ? null : modContent,
            isBinary);
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
        return ToOperationResult(res);
    }
}
