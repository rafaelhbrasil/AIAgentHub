using AIAgentHub.Application.FileChanges;
using AIAgentHub.Domain.FileChanges;
using AIAgentHub.Domain.Repositories;

using NSubstitute;

namespace AgentHub.UnitTests.Application.FileChanges;

public sealed class FileChangeServiceTests
{
    [Fact]
    public async Task FileChangeService_Operations_ShouldWork()
    {
        var changeRepo = Substitute.For<IFileChangeRepository>();
        var snapshotSvc = Substitute.For<ISnapshotService>();
        var diffEngine = Substitute.For<IDiffEngine>();

        _ = diffEngine.CalculateTextDiff(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>())
            .Returns(new DiffResult("src/Program.cs", false, true, 1, 0, new List<DiffLine>(), new List<SideBySideLine>()));
        _ = diffEngine.CalculateImageDiff(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>())
            .Returns(new DiffResult("logo.png", true, true, 0, 0, new List<DiffLine>(), new List<SideBySideLine>()));

        var service = new FileChangeService(changeRepo, snapshotSvc, diffEngine);

        var convId = Guid.NewGuid();
        var change = FileChange.Create(convId, "src/Program.cs", FileChangeType.Modified);
        _ = changeRepo.GetByIdAsync(change.Id, Arg.Any<CancellationToken>()).Returns(change);
        _ = changeRepo.GetByConversationIdAsync(convId, Arg.Any<CancellationToken>()).Returns(new List<FileChange> { change });

        var changes = await service.GetChangesAsync(convId);
        _ = Assert.Single(changes);

        var fetched = await service.GetByIdAsync(change.Id);
        Assert.NotNull(fetched);

        await service.AcceptAsync(change.Id);
        Assert.Equal(ReviewStatus.Accepted, change.Status);

        await service.RejectAsync(change.Id, Path.GetTempPath());
        Assert.Equal(ReviewStatus.Rejected, change.Status);

        _ = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.AcceptAsync(Guid.NewGuid()));
        _ = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.RejectAsync(Guid.NewGuid(), Path.GetTempPath()));

        var diff = await service.GetDiffAsync(change.Id, Path.GetTempPath());
        Assert.NotNull(diff);

        // Image file diff test
        var imgChange = FileChange.Create(convId, "logo.png", FileChangeType.Modified);
        _ = changeRepo.GetByIdAsync(imgChange.Id, Arg.Any<CancellationToken>()).Returns(imgChange);
        var imgDiff = await service.GetDiffAsync(imgChange.Id, Path.GetTempPath());
        Assert.NotNull(imgDiff);
    }
}
