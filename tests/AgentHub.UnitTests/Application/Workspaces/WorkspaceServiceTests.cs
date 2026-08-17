using AIAgentHub.Application.Filesystem;
using AIAgentHub.Application.Workspaces;
using AIAgentHub.Domain.Repositories;
using AIAgentHub.Domain.Workspaces;

using NSubstitute;

namespace AgentHub.UnitTests.Application.Workspaces;

public sealed class WorkspaceServiceTests
{
    [Fact]
    public async Task WorkspaceService_CrudOperations_ShouldWork()
    {
        var repo = Substitute.For<IWorkspaceRepository>();
        var fs = Substitute.For<IFilesystemService>();
        var validator = Substitute.For<ISystemPathValidator>();
        var service = new WorkspaceService(repo, fs, validator);

        var ws1 = Workspace.Create("WS1", Path.GetTempPath());
        _ = repo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Workspace> { ws1 });
        _ = repo.GetByIdAsync(ws1.Id, Arg.Any<CancellationToken>()).Returns(ws1);

        var all = await service.GetAllAsync();
        _ = Assert.Single(all);
        Assert.Equal("WS1", all[0].Name);

        var found = await service.GetByIdAsync(ws1.Id);
        Assert.NotNull(found);
        Assert.Equal("WS1", found!.Name);

        var notFound = await service.GetByIdAsync(Guid.NewGuid());
        Assert.Null(notFound);

        // Create new
        _ = fs.SuggestWorkspaceName(Arg.Any<string>()).Returns("SuggestedName");
        var createReq = new CreateWorkspaceRequest(null!, Path.GetTempPath(), WorkspaceOrigin.Server, "gemini", "gemini-2.5-pro");
        var created = await service.CreateAsync(createReq);
        Assert.Equal("SuggestedName", created.Name);

        // Create existing path -> touches
        _ = repo.GetByPathAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ws1);
        var existingResult = await service.CreateAsync(new CreateWorkspaceRequest("WS1", Path.GetTempPath()));
        Assert.Equal(ws1.Id, existingResult.Id);
        await repo.Received(1).UpdateAsync(ws1, Arg.Any<CancellationToken>());

        // Update
        var updateReq = new UpdateWorkspaceRequest("RenamedWS", new WorkspaceSettings { DefaultProviderId = "claude" });
        var updated = await service.UpdateAsync(ws1.Id, updateReq);
        Assert.Equal("RenamedWS", updated.Name);

        _ = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.UpdateAsync(Guid.NewGuid(), updateReq));

        // Delete & Touch
        await service.DeleteAsync(ws1.Id);
        await repo.Received(1).DeleteAsync(ws1.Id, Arg.Any<CancellationToken>());

        await service.TouchAsync(ws1.Id);
        await service.TouchAsync(Guid.NewGuid());
    }

    [Fact]
    public async Task WorkspaceService_CreateAsync_ForbiddenPath_ShouldThrowArgumentException()
    {
        var repo = Substitute.For<IWorkspaceRepository>();
        var fs = Substitute.For<IFilesystemService>();
        var validator = Substitute.For<ISystemPathValidator>();

        string? reasonOut = "Directory is a protected system folder.";
        validator.IsPathForbidden(Arg.Any<string>(), out Arg.Any<string?>())
            .Returns(x =>
            {
                x[1] = reasonOut;
                return true;
            });

        var service = new WorkspaceService(repo, fs, validator);
        var request = new CreateWorkspaceRequest("WindowsWS", @"C:\Windows", WorkspaceOrigin.Server);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(request));
        Assert.Contains("protected system folder", ex.Message);
    }
}
