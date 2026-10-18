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
}
