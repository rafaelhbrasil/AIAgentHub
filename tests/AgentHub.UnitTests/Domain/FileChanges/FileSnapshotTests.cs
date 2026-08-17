using AIAgentHub.Domain.FileChanges;

namespace AgentHub.UnitTests.Domain.FileChanges;

public sealed class FileSnapshotTests
{
    [Fact]
    public void FileSnapshot_Create_PathNormalization()
    {
        var wsId = Guid.NewGuid();
        var convId = Guid.NewGuid();
        var snap = FileSnapshot.Create(wsId, convId, "\\src\\App.cs", "key-123", "hash-abc", 1024);

        Assert.Equal(wsId, snap.WorkspaceId);
        Assert.Equal(convId, snap.ConversationId);
        Assert.Equal("src/App.cs", snap.RelativePath);
        Assert.Equal("key-123", snap.StorageKey);
        Assert.Equal("hash-abc", snap.FileHash);
        Assert.Equal(1024, snap.Size);
    }
}
