using AIAgentHub.Domain.FileChanges;

namespace AgentHub.UnitTests.Domain.FileChanges;

public sealed class FileChangeTests
{
    [Fact]
    public void FileChange_Create_Accept_Reject()
    {
        var convId = Guid.NewGuid();
        var change = FileChange.Create(convId, "\\src\\Program.cs", FileChangeType.Modified, "snapshot/path");

        Assert.Equal("src/Program.cs", change.RelativePath);
        Assert.Equal(FileChangeType.Modified, change.ChangeType);
        Assert.Equal("snapshot/path", change.SnapshotPath);
        Assert.Equal(ReviewStatus.Pending, change.Status);
        Assert.Null(change.ReviewedAtUtc);

        change.Accept();
        Assert.Equal(ReviewStatus.Accepted, change.Status);
        _ = Assert.NotNull(change.ReviewedAtUtc);

        change.Reject();
        Assert.Equal(ReviewStatus.Rejected, change.Status);

        _ = Assert.Throws<ArgumentException>(() => FileChange.Create(Guid.Empty, "src/path", FileChangeType.Created));
        _ = Assert.Throws<ArgumentException>(() => FileChange.Create(convId, "", FileChangeType.Created));
    }
}
