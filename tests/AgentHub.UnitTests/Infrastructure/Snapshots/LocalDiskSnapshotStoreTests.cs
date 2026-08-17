using AIAgentHub.Domain.FileChanges;
using AIAgentHub.Domain.Repositories;
using AIAgentHub.Infrastructure.Snapshots;

namespace AgentHub.UnitTests.Infrastructure.Snapshots;

public sealed class LocalDiskSnapshotStoreTests
{
    private sealed class FakeSnapshotRepo : IFileSnapshotRepository
    {
        private readonly List<FileSnapshot> _list = [];
        public Task AddAsync(FileSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            _list.Add(snapshot);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<FileSnapshot>> GetByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FileSnapshot>>(_list.Where(s => s.ConversationId == conversationId).ToList());
        public Task<FileSnapshot?> GetLatestByPathAsync(Guid workspaceId, string relativePath, CancellationToken cancellationToken = default) => Task.FromResult(_list.LastOrDefault(s => s.WorkspaceId == workspaceId && s.RelativePath == relativePath));
    }

    private sealed class FakeChangeRepo : IFileChangeRepository
    {
        private readonly List<FileChange> _list = [];
        public Task AddAsync(FileChange change, CancellationToken cancellationToken = default)
        {
            _list.Add(change);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<FileChange>> GetByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FileChange>>(_list.Where(c => c.ConversationId == conversationId).ToList());
        public Task<FileChange?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(_list.FirstOrDefault(c => c.Id == id));
        public Task UpdateAsync(FileChange change, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(FileChange change, CancellationToken cancellationToken = default)
        {
            _ = _list.Remove(change);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task SnapshotStore_Rollback_ShouldRestoreModifiedFile()
    {
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "AgentHubTestWs_" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(tempWorkspace);
        var testFile = Path.Combine(tempWorkspace, "test.txt");

        try
        {
            await File.WriteAllTextAsync(testFile, "Baseline Original Content");

            var fakeSnapshots = new FakeSnapshotRepo();
            var fakeChanges = new FakeChangeRepo();
            var store = new LocalDiskSnapshotStore(fakeSnapshots, fakeChanges);

            var wsId = Guid.NewGuid();
            var convId = Guid.NewGuid();

            var token = await store.CaptureWorkspaceSnapshotAsync(wsId, convId, tempWorkspace, Array.Empty<string>());
            Assert.NotEmpty(token);

            // Simulate file modification by AI
            await File.WriteAllTextAsync(testFile, "Modified Content by AI");

            var detected = await store.DetectAndRecordChangesAsync(wsId, convId, tempWorkspace, token, Array.Empty<string>());
            _ = Assert.Single(detected);
            Assert.Equal(FileChangeType.Modified, detected[0].ChangeType);

            // Test Reject & Rollback
            await store.RollbackFileAsync(detected[0], tempWorkspace);

            var rolledBackContent = await File.ReadAllTextAsync(testFile);
            Assert.Equal("Baseline Original Content", rolledBackContent);
        }
        finally
        {
            if (Directory.Exists(tempWorkspace))
            {
                Directory.Delete(tempWorkspace, true);
            }
        }
    }

    [Fact]
    public async Task LocalDiskSnapshotStore_MultiPrompt_PreservesOriginalBaselineAndPreventsDuplicates()
    {
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "SnapMultiPromptWs_" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(tempWorkspace);

        try
        {
            var wsId = Guid.NewGuid();
            var convId = Guid.NewGuid();
            var testFilePath = Path.Combine(tempWorkspace, "Example.cs");
            await File.WriteAllTextAsync(testFilePath, "Initial content A");

            var snapRepo = new FakeSnapshotRepo();
            var changeRepo = new FakeChangeRepo();
            var store = new LocalDiskSnapshotStore(snapRepo, changeRepo);

            // 1. Prompt 1 starts: Capture initial baseline A
            var token1 = await store.CaptureWorkspaceSnapshotAsync(wsId, convId, tempWorkspace, []);

            // AI modifies A -> B
            await File.WriteAllTextAsync(testFilePath, "Modified content B");

            // Prompt 1 finishes: Detect changes
            var changes1 = await store.DetectAndRecordChangesAsync(wsId, convId, tempWorkspace, token1, []);
            Assert.Single(changes1);
            Assert.Equal("Example.cs", changes1[0].RelativePath);
            Assert.Equal(FileChangeType.Modified, changes1[0].ChangeType);

            // Verify snapshot in repo is A
            var initialSnapshot = (await snapRepo.GetByConversationIdAsync(convId)).Single();

            // 2. Prompt 2 starts before approving: Capture pre-prompt snapshot
            // Since Example.cs is pending, it should NOT overwrite baseline A!
            var token2 = await store.CaptureWorkspaceSnapshotAsync(wsId, convId, tempWorkspace, []);

            // AI modifies B -> C
            await File.WriteAllTextAsync(testFilePath, "Further modified content C");

            // Prompt 2 finishes: Detect changes
            var changes2 = await store.DetectAndRecordChangesAsync(wsId, convId, tempWorkspace, token2, []);

            // Should still only have 1 pending change for Example.cs pointing to initial baseline A
            Assert.Single(changes2);
            var allChanges = await changeRepo.GetByConversationIdAsync(convId);
            Assert.Single(allChanges);
            Assert.Equal(initialSnapshot.StorageKey, allChanges[0].SnapshotPath);

            // 3. Prompt 3 reverts file back to initial content A
            var token3 = await store.CaptureWorkspaceSnapshotAsync(wsId, convId, tempWorkspace, []);
            await File.WriteAllTextAsync(testFilePath, "Initial content A");

            var changes3 = await store.DetectAndRecordChangesAsync(wsId, convId, tempWorkspace, token3, []);
            Assert.Empty(changes3);
            var remainingChanges = await changeRepo.GetByConversationIdAsync(convId);
            Assert.Empty(remainingChanges);
        }
        finally
        {
            if (Directory.Exists(tempWorkspace))
            {
                Directory.Delete(tempWorkspace, true);
            }
        }
    }
}
