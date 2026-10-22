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
                    return args.Contains("rev-parse --is-inside-work-tree", StringComparison.Ordinal)
                        ? new ProcessCommandResult(128, "", "fatal: not a git repository")
                        : new ProcessCommandResult(0, "", "");
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
                    if (args.Contains("rev-parse --is-inside-work-tree", StringComparison.Ordinal))
                    {
                        return new ProcessCommandResult(0, "true\n", "");
                    }
                    if (args.Contains("branch --show-current", StringComparison.Ordinal))
                    {
                        return new ProcessCommandResult(0, "feature/v0.3\n", "");
                    }
                    if (args.Contains("status --porcelain", StringComparison.Ordinal))
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
            _ = Assert.Single(status.Staged);
            Assert.Equal("src/file1.cs", status.Staged[0].Path);
            _ = Assert.Single(status.Modified);
            Assert.Equal("src/file2.cs", status.Modified[0].Path);
            _ = Assert.Single(status.Untracked);
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

        _ = Assert.Single(history);
        var commit = history[0];
        Assert.Equal("abcd1234efgh5678", commit.Hash);
        Assert.Equal("abcd123", commit.ShortHash);
        Assert.Equal("Rafael", commit.Author);
        Assert.Equal("rafael@example.com", commit.Email);
        Assert.Equal("feat: add git support", commit.Message);
    }

    [Fact]
    public async Task GetHistoryAsync_RequestsFullParentHashes()
    {
        // Regression: abbreviated parents (%p) never matched full Hash values (%H)
        // in the frontend commit graph lane matcher, producing one lane per commit.
        string? capturedArgs = null;
        var fakeExecutor = new FakeProcessExecutor
        {
            OnRunCommand = (exe, args, dir) =>
            {
                capturedArgs = args;
                return new ProcessCommandResult(0, string.Empty, "");
            }
        };

        var service = new GitCliService(fakeExecutor);
        _ = await service.GetHistoryAsync("C:\\dummy");

        Assert.NotNull(capturedArgs);
        Assert.Contains("%P", capturedArgs, StringComparison.Ordinal);
        Assert.DoesNotContain("%p", capturedArgs, StringComparison.Ordinal);
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

        _ = Assert.Single(history);
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
                if (args.Contains("show -s --format=", StringComparison.Ordinal))
                {
                    var meta = "1234567890abcdef|||1234567|||Rafael|||rafael@example.com|||2026-10-18T12:00:00+00:00|||feat: support git log\nDetailed body description|||parent123||| (HEAD -> develop)";
                    return new ProcessCommandResult(0, meta, "");
                }
                if (args.Contains("show --numstat", StringComparison.Ordinal))
                {
                    var numstat = "15\t3\tsrc/FileA.cs\n-\t-\tassets/logo.png\n";
                    return new ProcessCommandResult(0, numstat, "");
                }
                if (args.Contains("show --name-status", StringComparison.Ordinal))
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
        _ = Assert.Single(details.Parents);
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
                if (args.Contains("~1:src/FileA.cs", StringComparison.Ordinal))
                {
                    return new ProcessCommandResult(0, "old content", "");
                }
                return args.Contains(":src/FileA.cs", StringComparison.Ordinal)
                    ? new ProcessCommandResult(0, "new content", "")
                    : new ProcessCommandResult(1, "", "not found");
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

    [Fact]
    public async Task GetCommitDiffAsync_BinaryContent_ReturnsIsBinaryTrue()
    {
        var fakeExecutor = new FakeProcessExecutor
        {
            OnRunCommand = (exe, args, dir) =>
            {
                if (args.Contains(":asset.bin", StringComparison.Ordinal))
                {
                    return new ProcessCommandResult(0, "header\0binarydata", "");
                }
                return new ProcessCommandResult(1, "", "not found");
            }
        };

        var service = new GitCliService(fakeExecutor);
        var diff = await service.GetCommitDiffAsync("C:\\dummy", "1234abcd", "asset.bin");

        Assert.NotNull(diff);
        Assert.True(diff.IsBinary);
        Assert.Null(diff.OriginalContent);
        Assert.Null(diff.ModifiedContent);
    }

    [Fact]
    public async Task CreateBranchAsync_ValidBranch_RunsCheckoutOrBranch()
    {
        var fakeExecutor = new FakeProcessExecutor
        {
            OnRunCommand = (exe, args, dir) => new ProcessCommandResult(0, "Switched to a new branch 'feature/test'", "")
        };

        var service = new GitCliService(fakeExecutor);
        var resultCheckout = await service.CreateBranchAsync("C:\\dummy", "feature/test", checkout: true);
        Assert.True(resultCheckout.Success);

        var resultNoCheckout = await service.CreateBranchAsync("C:\\dummy", "feature/test", checkout: false);
        Assert.True(resultNoCheckout.Success);

        var resultEmpty = await service.CreateBranchAsync("C:\\dummy", "  ");
        Assert.False(resultEmpty.Success);
    }

    [Fact]
    public async Task CheckoutBranchAsync_ValidBranch_RunsCheckout()
    {
        var fakeExecutor = new FakeProcessExecutor
        {
            OnRunCommand = (exe, args, dir) => new ProcessCommandResult(0, "Switched to branch 'develop'", "")
        };

        var service = new GitCliService(fakeExecutor);
        var result = await service.CheckoutBranchAsync("C:\\dummy", "develop");
        Assert.True(result.Success);

        var resultEmpty = await service.CheckoutBranchAsync("C:\\dummy", "");
        Assert.False(resultEmpty.Success);
    }

    [Fact]
    public async Task StageAsync_FilesOrAll_ExecutesCorrectCommand()
    {
        var executedArgs = new List<string>();
        var fakeExecutor = new FakeProcessExecutor
        {
            OnRunCommand = (exe, args, dir) =>
            {
                executedArgs.Add(args);
                return new ProcessCommandResult(0, "", "");
            }
        };

        var service = new GitCliService(fakeExecutor);
        var resAll = await service.StageAsync("C:\\dummy", stageAll: true);
        Assert.True(resAll.Success);
        Assert.Contains("add -A", executedArgs[0], StringComparison.Ordinal);

        var resFiles = await service.StageAsync("C:\\dummy", ["file1.cs", "file2.cs"]);
        Assert.True(resFiles.Success);
        Assert.Contains("add -- \"file1.cs\" \"file2.cs\"", executedArgs[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnstageAsync_FilesOrAll_ExecutesCorrectCommandAndHandlesFallback()
    {
        var executedArgs = new List<string>();
        var fakeExecutor = new FakeProcessExecutor
        {
            OnRunCommand = (exe, args, dir) =>
            {
                executedArgs.Add(args);
                if (args.StartsWith("restore --staged", StringComparison.Ordinal) && args.Contains("could-fail", StringComparison.Ordinal))
                {
                    return new ProcessCommandResult(1, "", "fatal: could not resolve 'HEAD'");
                }
                return new ProcessCommandResult(0, "", "");
            }
        };

        var service = new GitCliService(fakeExecutor);
        var resAll = await service.UnstageAsync("C:\\dummy", unstageAll: true);
        Assert.True(resAll.Success);

        var resFiles = await service.UnstageAsync("C:\\dummy", ["could-fail.cs"]);
        Assert.True(resFiles.Success); // Falls back to rm --cached and succeeds
        Assert.Contains(executedArgs, a => a.Contains("rm --cached", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CommitAsync_ValidMessage_CreatesCommit()
    {
        var fakeExecutor = new FakeProcessExecutor
        {
            OnRunCommand = (exe, args, dir) => new ProcessCommandResult(0, "[develop 1234567] feat: commit", "")
        };

        var service = new GitCliService(fakeExecutor);
        var resEmpty = await service.CommitAsync("C:\\dummy", "");
        Assert.False(resEmpty.Success);

        var resValid = await service.CommitAsync("C:\\dummy", "feat: commit", stageAll: true);
        Assert.True(resValid.Success);
    }

    [Fact]
    public async Task PushPullFetchStash_ExecuteExpectedCommands()
    {
        var commands = new List<string>();
        var fakeExecutor = new FakeProcessExecutor
        {
            OnRunCommand = (exe, args, dir) =>
            {
                commands.Add(args);
                return new ProcessCommandResult(0, "ok", "");
            }
        };

        var service = new GitCliService(fakeExecutor);
        var pushRes = await service.PushAsync("C:\\dummy", "origin", "develop");
        Assert.True(pushRes.Success);

        var pullRes = await service.PullAsync("C:\\dummy");
        Assert.True(pullRes.Success);

        var fetchRes = await service.FetchAsync("C:\\dummy");
        Assert.True(fetchRes.Success);

        var stashSave = await service.StashAsync("C:\\dummy", "save");
        Assert.True(stashSave.Success);

        var stashPop = await service.StashAsync("C:\\dummy", "pop");
        Assert.True(stashPop.Success);

        Assert.Equal(5, commands.Count);
    }

    [Fact]
    public async Task CloneAsync_ValidatesInputAndRunsClone()
    {
        var fakeExecutor = new FakeProcessExecutor
        {
            OnRunCommand = (exe, args, dir) => new ProcessCommandResult(0, "Cloning into...", "")
        };

        var service = new GitCliService(fakeExecutor);
        var emptyUrl = await service.CloneAsync("", "C:\\temp\\dest");
        Assert.False(emptyUrl.Success);

        var emptyDir = await service.CloneAsync("https://example.com/repo.git", "");
        Assert.False(emptyDir.Success);

        var valid = await service.CloneAsync("https://example.com/repo.git", Path.Combine(Path.GetTempPath(), "dest-clone"));
        Assert.True(valid.Success);
    }

    [Fact]
    public async Task GetStatusAsync_MapStatusCode_CoversAllStatusFlags()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"GitTest_{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(tempDir);
        try
        {
            var fakeExecutor = new FakeProcessExecutor
            {
                OnRunCommand = (exe, args, dir) =>
                {
                    if (args.Contains("--is-inside-work-tree", StringComparison.Ordinal))
                        return new ProcessCommandResult(0, "true", "");
                    if (args.Contains("status --porcelain", StringComparison.Ordinal))
                    {
                        // Covers A, D, R, C, U, ?, and unknown X
                        return new ProcessCommandResult(0, "## main...origin/main [ahead 1]\nA  fileA.txt\nD  fileD.txt\nR  fileR.txt\nC  fileC.txt\nUU fileU.txt\n?? fileUntracked.txt\nX  fileX.txt\n", "");
                    }
                    return new ProcessCommandResult(0, "", "");
                }
            };

            var service = new GitCliService(fakeExecutor);
            var status = await service.GetStatusAsync(tempDir);

            Assert.True(status.IsRepository);
            Assert.NotEmpty(status.Staged);
            Assert.Contains(status.Staged, f => f.Status == "Added");
            Assert.Contains(status.Staged, f => f.Status == "Deleted");
            Assert.Contains(status.Staged, f => f.Status == "Renamed");
            Assert.Contains(status.Staged, f => f.Status == "Copied");
            Assert.Contains(status.Modified, f => f.Status == "Updated");
            Assert.Contains(status.Staged, f => f.Status == "X");
            Assert.Contains("fileUntracked.txt", status.Untracked);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task GetCommitDetailAsync_HandlesBinaryFilesAndInvalidMetadata()
    {
        var hexHash = "abcdef123456";
        var fakeExecutor = new FakeProcessExecutor
        {
            OnRunCommand = (exe, args, dir) =>
            {
                if (args.Contains("show -s --format=", StringComparison.Ordinal))
                {
                    // Valid metadata format
                    return new ProcessCommandResult(0, $"{hexHash}|||abc1234|||Author Name|||author@test.com|||2026-10-20T00:00:00Z|||Commit subject|||parent1", "");
                }
                if (args.Contains("show --numstat", StringComparison.Ordinal))
                {
                    // Binary files use - - filename
                    return new ProcessCommandResult(0, "-\t-\timage.png\nM\timage.png\n10\t5\tfile.cs\nM\tfile.cs", "");
                }
                return new ProcessCommandResult(0, "", "");
            }
        };

        var service = new GitCliService(fakeExecutor);
        var detail = await service.GetCommitDetailsAsync("C:\\dummy", hexHash);

        Assert.NotNull(detail);
        Assert.Equal(hexHash, detail.Hash);
        Assert.Equal(2, detail.Files.Count);

        // Test with invalid metadata (< 6 parts)
        var invalidExecutor = new FakeProcessExecutor
        {
            OnRunCommand = (exe, args, dir) => new ProcessCommandResult(0, "short|||meta", "")
        };
        var invalidService = new GitCliService(invalidExecutor);
        var nullDetail = await invalidService.GetCommitDetailsAsync("C:\\dummy", hexHash);
        Assert.Null(nullDetail);
    }

    [Fact]
    public async Task GitOperations_FailureOutput_FallsBackToStdoutWhenStderrEmpty()
    {
        var fakeExecutor = new FakeProcessExecutor
        {
            OnRunCommand = (exe, args, dir) => new ProcessCommandResult(1, "Stdout error message", "")
        };

        var service = new GitCliService(fakeExecutor);
        var branchRes = await service.CreateBranchAsync("C:\\dummy", "bad*branch");
        Assert.False(branchRes.Success);
        Assert.Equal("Stdout error message", branchRes.Error);
    }
}
