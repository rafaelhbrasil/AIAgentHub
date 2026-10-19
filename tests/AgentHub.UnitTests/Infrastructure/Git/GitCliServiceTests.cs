using AIAgentHub.Application.Git;
using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Infrastructure.Executors;
using AIAgentHub.Infrastructure.Git;

namespace AgentHub.UnitTests.Infrastructure.Git;

public sealed class GitCliServiceTests
{
    private sealed class FakeProcessExecutor : IProcessExecutor
    {
        public Func<string, string, string?, ProcessCommandResult>? OnRunCommand { get; set; }

        public Task<ProcessCommandResult> RunCommandAsync(string executable, string arguments, string? workingDirectory = null, CancellationToken cancellationToken = default, string? operationTitle = null)
        {
            var res = OnRunCommand != null
                ? OnRunCommand(executable, arguments, workingDirectory)
                : new ProcessCommandResult(0, string.Empty, string.Empty);
            return Task.FromResult(res);
        }

        public Task ExecuteAsync(string displayName, string executableName, string arguments, ProviderExecutionContext context, IPromptLogger promptLogger, CliExecutionOptions options)
            => Task.CompletedTask;

        public bool AbortProcess(Guid conversationId) => true;
        public bool IsRunning(Guid conversationId) => false;
    }

    [Fact]
    public async Task GetStatusAsync_WhenNotInsideGitRepo_ReturnsNotRepository()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"GitTest_{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(tempDir);
        try
        {
            var fakeExecutor = new FakeProcessExecutor
            {
                OnRunCommand = (exe, args, dir) =>
                {
                    if (args.Contains("rev-parse --is-inside-work-tree"))
                    {
                        return new ProcessCommandResult(128, "", "fatal: not a git repository");
                    }
                    return new ProcessCommandResult(0, "", "");
                }
            };

            var service = new GitCliService(fakeExecutor);
            var status = await service.GetStatusAsync(tempDir);

            Assert.True(status.IsGitInstalled);
            Assert.False(status.IsRepository);
            Assert.Null(status.CurrentBranch);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task GetStatusAsync_WhenInsideGitRepo_ParsesPorcelainStatusProperly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"GitTest_{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(tempDir);
        try
        {
            var fakeExecutor = new FakeProcessExecutor
            {
                OnRunCommand = (exe, args, dir) =>
                {
                    if (args.Contains("rev-parse --is-inside-work-tree"))
                    {
                        return new ProcessCommandResult(0, "true\n", "");
                    }
                    if (args.Contains("branch --show-current"))
                    {
                        return new ProcessCommandResult(0, "feature/v0.3\n", "");
                    }
                    if (args.Contains("status --porcelain"))
                    {
                        var porcelain = "## feature/v0.3...origin/feature/v0.3 [ahead 2, behind 1]\n" +
                                        "M  src/file1.cs\n" +
                                        " M src/file2.cs\n" +
                                        "?? untracked.txt\n";
                        return new ProcessCommandResult(0, porcelain, "");
                    }
                    return new ProcessCommandResult(0, "", "");
                }
            };

            var service = new GitCliService(fakeExecutor);
            var status = await service.GetStatusAsync(tempDir);

            Assert.True(status.IsGitInstalled);
            Assert.True(status.IsRepository);
            Assert.Equal("feature/v0.3", status.CurrentBranch);
            Assert.Equal(2, status.Ahead);
            Assert.Equal(1, status.Behind);
            Assert.Single(status.Staged);
            Assert.Equal("src/file1.cs", status.Staged[0].Path);
            Assert.Single(status.Modified);
            Assert.Equal("src/file2.cs", status.Modified[0].Path);
            Assert.Single(status.Untracked);
            Assert.Equal("untracked.txt", status.Untracked[0]);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task GetBranchesAsync_ParsesBranchListingCorrectly()
    {
        var fakeExecutor = new FakeProcessExecutor
        {
            OnRunCommand = (exe, args, dir) =>
            {
                var branchOutput = "  develop\n" +
                                   "* feature/v0.3\n" +
                                   "  main\n" +
                                   "  remotes/origin/HEAD -> origin/main\n" +
                                   "  remotes/origin/main\n";
                return new ProcessCommandResult(0, branchOutput, "");
            }
        };

        var service = new GitCliService(fakeExecutor);
        var branches = await service.GetBranchesAsync("C:\\dummy");

        Assert.Equal(4, branches.Count);
        var current = branches.First(b => b.IsCurrent);
        Assert.Equal("feature/v0.3", current.Name);
        Assert.False(current.IsRemote);

        var remote = branches.First(b => b.IsRemote);
        Assert.Equal("origin/main", remote.Name);
    }

    [Fact]
    public async Task GetHistoryAsync_ParsesLogEntries()
    {
        var fakeExecutor = new FakeProcessExecutor
        {
            OnRunCommand = (exe, args, dir) =>
            {
                var logLine = "abcd1234efgh5678|||abcd123|||Rafael|||rafael@example.com|||2026-10-05T12:00:00+00:00|||feat: add git support\n";
                return new ProcessCommandResult(0, logLine, "");
            }
        };

        var service = new GitCliService(fakeExecutor);
        var history = await service.GetHistoryAsync("C:\\dummy");

        Assert.Single(history);
        var commit = history[0];
        Assert.Equal("abcd1234efgh5678", commit.Hash);
        Assert.Equal("abcd123", commit.ShortHash);
        Assert.Equal("Rafael", commit.Author);
        Assert.Equal("rafael@example.com", commit.Email);
        Assert.Equal("feat: add git support", commit.Message);
    }

    [Fact]
    public async Task GetHistoryAsync_ParsesParentsAndRefs()
    {
        var fakeExecutor = new FakeProcessExecutor
        {
            OnRunCommand = (exe, args, dir) =>
            {
                var logLine = "abcd1234efgh5678|||abcd123|||Rafael|||rafael@example.com|||2026-10-05T12:00:00+00:00|||merge: pr 42|||parent1 parent2||| (HEAD -> develop, origin/develop)\n";
                return new ProcessCommandResult(0, logLine, "");
            }
        };

        var service = new GitCliService(fakeExecutor);
        var history = await service.GetHistoryAsync("C:\\dummy");

        Assert.Single(history);
        var commit = history[0];
        Assert.NotNull(commit.Parents);
        Assert.Equal(2, commit.Parents.Count);
        Assert.Equal("parent1", commit.Parents[0]);
        Assert.Equal("parent2", commit.Parents[1]);
        Assert.Equal("(HEAD -> develop, origin/develop)", commit.Refs);
    }

    [Fact]
    public async Task GetCommitDetailsAsync_ValidCommit_ParsesMetadataAndFiles()
    {
        var fakeExecutor = new FakeProcessExecutor
        {
            OnRunCommand = (exe, args, dir) =>
            {
                if (args.Contains("show -s --format="))
                {
                    var meta = "1234567890abcdef|||1234567|||Rafael|||rafael@example.com|||2026-10-18T12:00:00+00:00|||feat: support git log\nDetailed body description|||parent123||| (HEAD -> develop)";
                    return new ProcessCommandResult(0, meta, "");
                }
                if (args.Contains("show --numstat"))
                {
                    var numstat = "15\t3\tsrc/FileA.cs\n-\t-\tassets/logo.png\n";
                    return new ProcessCommandResult(0, numstat, "");
                }
                if (args.Contains("show --name-status"))
                {
                    var nameStatus = "M\tsrc/FileA.cs\nA\tassets/logo.png\n";
                    return new ProcessCommandResult(0, nameStatus, "");
                }
                return new ProcessCommandResult(0, "", "");
            }
        };

        var service = new GitCliService(fakeExecutor);
        var details = await service.GetCommitDetailsAsync("C:\\dummy", "1234567890abcdef");

        Assert.NotNull(details);
        Assert.Equal("1234567890abcdef", details.Hash);
        Assert.Equal("1234567", details.ShortHash);
        Assert.Equal("Rafael", details.Author);
        Assert.Equal("feat: support git log\nDetailed body description", details.Message);
        Assert.Single(details.Parents);
        Assert.Equal("parent123", details.Parents[0]);
        Assert.Equal(2, details.Files.Count);

        var fileA = details.Files.First(f => f.Path == "src/FileA.cs");
        Assert.Equal("Modified", fileA.Status);
        Assert.Equal(15, fileA.Additions);
        Assert.Equal(3, fileA.Deletions);

        var logo = details.Files.First(f => f.Path == "assets/logo.png");
        Assert.Equal("Added", logo.Status);
        Assert.Equal(0, logo.Additions);
        Assert.Equal(0, logo.Deletions);
    }

    [Fact]
    public async Task GetCommitDetailsAsync_InvalidHashOrPath_ReturnsNull()
    {
        var fakeExecutor = new FakeProcessExecutor();
        var service = new GitCliService(fakeExecutor);

        Assert.Null(await service.GetCommitDetailsAsync("", "123456"));
        Assert.Null(await service.GetCommitDetailsAsync("C:\\dummy", ""));
        Assert.Null(await service.GetCommitDetailsAsync("C:\\dummy", "not a valid hash! ; rm -rf"));
    }

    [Fact]
    public async Task GetCommitDiffAsync_ValidFile_ReturnsOriginalAndModifiedContent()
    {
        var fakeExecutor = new FakeProcessExecutor
        {
            OnRunCommand = (exe, args, dir) =>
            {
                if (args.Contains("~1:src/FileA.cs"))
                {
                    return new ProcessCommandResult(0, "old content", "");
                }
                if (args.Contains(":src/FileA.cs"))
                {
                    return new ProcessCommandResult(0, "new content", "");
                }
                return new ProcessCommandResult(1, "", "not found");
            }
        };

        var service = new GitCliService(fakeExecutor);
        var diff = await service.GetCommitDiffAsync("C:\\dummy", "1234abcd", "src/FileA.cs");

        Assert.NotNull(diff);
        Assert.Equal("src/FileA.cs", diff.Path);
        Assert.Equal("old content", diff.OriginalContent);
        Assert.Equal("new content", diff.ModifiedContent);
        Assert.False(diff.IsBinary);
    }

    [Fact]
    public async Task GetCommitDiffAsync_PathTraversal_ReturnsNull()
    {
        var fakeExecutor = new FakeProcessExecutor();
        var service = new GitCliService(fakeExecutor);

        var diff = await service.GetCommitDiffAsync("C:\\dummy", "1234abcd", "../../../etc/passwd");
        Assert.Null(diff);
    }
}
