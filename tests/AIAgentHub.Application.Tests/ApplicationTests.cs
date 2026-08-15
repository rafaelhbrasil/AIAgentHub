using AIAgentHub.Application.Common;
using AIAgentHub.Application.Conversations;
using AIAgentHub.Application.Execution;
using AIAgentHub.Application.FileChanges;
using AIAgentHub.Application.Filesystem;
using AIAgentHub.Application.Providers;
using AIAgentHub.Application.Realtime;
using AIAgentHub.Application.Rendering;
using AIAgentHub.Application.Security;
using AIAgentHub.Application.Workspaces;
using AIAgentHub.Domain.Conversations;
using AIAgentHub.Domain.FileChanges;
using AIAgentHub.Domain.Permissions;
using AIAgentHub.Domain.Providers;
using AIAgentHub.Domain.Repositories;
using AIAgentHub.Domain.Workspaces;

using NSubstitute;

namespace AIAgentHub.Application.Tests;

public sealed class ApplicationTests
{
    [Fact]
    public void Result_And_GenericResult_ShouldBehaveCorrectly()
    {
        var ok = Result.Ok();
        Assert.True(ok.Success);
        Assert.Null(ok.Error);

        var fail = Result.Fail("Failed error");
        Assert.False(fail.Success);
        Assert.Equal("Failed error", fail.Error);

        var genericOk = Result<string>.Ok("data payload");
        Assert.True(genericOk.Success);
        Assert.Equal("data payload", genericOk.Data);
        Assert.Null(genericOk.Error);

        var genericFail = Result<int>.Fail("invalid number");
        Assert.False(genericFail.Success);
        Assert.Equal(0, genericFail.Data);
        Assert.Equal("invalid number", genericFail.Error);
    }

    [Fact]
    public async Task WorkspaceService_CrudOperations_ShouldWork()
    {
        var repo = Substitute.For<IWorkspaceRepository>();
        var fs = Substitute.For<IFilesystemService>();
        var service = new WorkspaceService(repo, fs);

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
    public async Task ConversationService_Operations_ShouldWork()
    {
        var convRepo = Substitute.For<IConversationRepository>();
        var wsRepo = Substitute.For<IWorkspaceRepository>();
        var service = new ConversationService(convRepo, wsRepo);

        var wsId = Guid.NewGuid();
        var ws = Workspace.Create("WS", Path.GetTempPath());
        _ = wsRepo.GetByIdAsync(wsId, Arg.Any<CancellationToken>()).Returns(ws);

        var conv = Conversation.Create(wsId, "Conv 1", "gemini");
        _ = convRepo.GetByWorkspaceIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new List<Conversation> { conv });
        _ = convRepo.GetByIdAsync(conv.Id, Arg.Any<CancellationToken>()).Returns(conv);

        var list = await service.GetByWorkspaceIdAsync(wsId);
        _ = Assert.Single(list);

        var detail = await service.GetByIdAsync(conv.Id);
        Assert.NotNull(detail);
        Assert.Equal("Conv 1", detail!.Title);

        var notFoundDetail = await service.GetByIdAsync(Guid.NewGuid());
        Assert.Null(notFoundDetail);

        // Create
        var createReq = new CreateConversationRequest(wsId, "New Conv", "claude", "claude-3-7-sonnet");
        var created = await service.CreateAsync(createReq);
        Assert.Equal("New Conv", created.Title);

        _ = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.CreateAsync(new CreateConversationRequest(Guid.NewGuid(), "Title")));

        // Rename
        var renamed = await service.RenameAsync(conv.Id, "Renamed Conv");
        Assert.Equal("Renamed Conv", renamed.Title);
        _ = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.RenameAsync(Guid.NewGuid(), "Title"));

        // SetProviderAndModel
        await service.SetProviderAndModelAsync(conv.Id, "opencode", "gpt-5", "high");
        Assert.Equal("opencode", conv.ProviderId);
        Assert.Equal("gpt-5", conv.ModelId);
        _ = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SetProviderAndModelAsync(Guid.NewGuid(), "provider", "model"));

        // AddMessage
        var msg = await service.AddMessageAsync(conv.Id, MessageRole.User, "User Prompt");
        Assert.Equal("User Prompt", msg.Content);
        _ = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.AddMessageAsync(Guid.NewGuid(), MessageRole.User, "Prompt"));

        // Delete
        await service.DeleteAsync(conv.Id);
        await convRepo.Received(1).DeleteAsync(conv.Id, Arg.Any<CancellationToken>());

        // Search
        _ = wsRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Workspace> { ws });
        var emptySearch = await service.SearchAsync("  ");
        Assert.Empty(emptySearch);

        var searchResult = await service.SearchAsync("Prompt");
        _ = Assert.Single(searchResult);
    }

    [Fact]
    public void DiffEngine_CalculateTextDiff_ShouldDetectAdditionsAndDeletions()
    {
        var engine = new DiffEngine();
        var oldText = "Line 1\nLine 2\nLine 3";
        var newText = "Line 1\nLine 2 modified\nLine 3\nLine 4 added";

        var diff = engine.CalculateTextDiff("test.txt", oldText, newText);

        Assert.True(diff.HasChanges);
        Assert.True(diff.AdditionsCount > 0);
        Assert.NotEmpty(diff.UnifiedLines);
        Assert.NotEmpty(diff.SideBySideLines);
    }

    [Fact]
    public async Task Renderers_ShouldFormatContentProperly()
    {
        var mdRenderer = new MarkdownContentRenderer();
        var mdResult = await mdRenderer.RenderAsync("file.md", System.Text.Encoding.UTF8.GetBytes("# Header\n```csharp\ncode\n```"));
        Assert.Equal("text/markdown", mdResult.ContentType);
        Assert.Contains("Header", mdResult.RenderedHtml);

        var jsonRenderer = new JsonContentRenderer();
        var jsonResult = await jsonRenderer.RenderAsync("file.json", System.Text.Encoding.UTF8.GetBytes(/*lang=json,strict*/ "{\"a\":1}"));
        Assert.Equal("application/json", jsonResult.ContentType);
        Assert.Contains("a", jsonResult.RenderedHtml);

        var xmlRenderer = new XmlContentRenderer();
        var xmlResult = await xmlRenderer.RenderAsync("file.xml", System.Text.Encoding.UTF8.GetBytes("<root><item>val</item></root>"));
        Assert.Equal("application/xml", xmlResult.ContentType);
        Assert.Contains("root", xmlResult.RenderedHtml);

        var yamlRenderer = new YamlContentRenderer();
        var yamlResult = await yamlRenderer.RenderAsync("file.yaml", System.Text.Encoding.UTF8.GetBytes("key: value"));
        Assert.Equal("application/x-yaml", yamlResult.ContentType);
        Assert.Contains("key", yamlResult.RenderedHtml);

        var textRenderer = new TextContentRenderer();
        var textResult = await textRenderer.RenderAsync("file.txt", System.Text.Encoding.UTF8.GetBytes("plain text"));
        Assert.Equal("text/plain", textResult.ContentType);

        var manager = new ContentRenderingManager(new IContentRenderer[] { mdRenderer, jsonRenderer, xmlRenderer, yamlRenderer, textRenderer });
        var managedResult = await manager.RenderFileAsync("unknown.xyz", System.Text.Encoding.UTF8.GetBytes("fallback text"));
        Assert.Equal("FallbackRenderer", managedResult.RendererName);
    }

    [Fact]
    public async Task FilesystemService_Operations_ShouldWork()
    {
        var service = new FilesystemService();
        var tempFolder = Path.GetTempPath();

        var suggested = service.SuggestWorkspaceName(tempFolder);
        Assert.NotEmpty(suggested);

        var drives = await service.GetDrivesAsync();
        Assert.NotEmpty(drives);

        var browse = await service.BrowseDirectoryAsync(tempFolder);
        Assert.NotNull(browse);

        var tree = await service.GetWorkspaceTreeAsync(tempFolder);
        Assert.NotNull(tree);

        var testFile = Path.Combine(tempFolder, $"fs_test_{Guid.NewGuid():N}.txt");
        try
        {
            await service.WriteFileTextAsync(testFile, "Hello Filesystem");
            Assert.True(service.FileExists(testFile));
            Assert.True(service.DirectoryExists(tempFolder));

            var readBackText = await service.ReadFileTextAsync(testFile);
            Assert.Equal("Hello Filesystem", readBackText);

            var readBackBytes = await service.ReadFileBytesAsync(testFile);
            Assert.NotEmpty(readBackBytes);

            await service.WriteFileBytesAsync(testFile, System.Text.Encoding.UTF8.GetBytes("Binary Data"));
            Assert.Equal("Binary Data", await service.ReadFileTextAsync(testFile));
        }
        finally
        {
            if (File.Exists(testFile))
            {
                File.Delete(testFile);
            }
        }
    }

    [Fact]
    public async Task SetupService_WipeAllDataAsync_ShouldInvokeDatabaseResetter()
    {
        var resetter = new TestDatabaseResetter();
        var setupService = new SetupService(null!, null!, null!, resetter);

        var result = await setupService.WipeAllDataAsync();

        Assert.True(result);
        Assert.True(resetter.WasWiped);
    }

    [Fact]
    public async Task ProviderManager_Operations_ShouldWork()
    {
        var provider = Substitute.For<IProvider>();
        _ = provider.Id.Returns("testprov");
        _ = provider.DisplayName.Returns("Test Provider");
        _ = provider.DetectAsync(Arg.Any<CancellationToken>()).Returns(new ProviderInfo { Id = "testprov", DisplayName = "Test Provider", IsInstalled = true });
        _ = provider.DetectDetailedAsync(Arg.Any<CancellationToken>()).Returns(new ProviderDetectionResult(ProviderStatus.Ready, "Ready", null));
        _ = provider.GetModelsAsync(Arg.Any<CancellationToken>()).Returns(new List<ModelInfo> { new() { Id = "m1", DisplayName = "M1" } });

        var modelSettingRepo = Substitute.For<IProviderModelSettingRepository>();
        var detectionRecordRepo = Substitute.For<IProviderDetectionRecordRepository>();

        var manager = new ProviderManager(
            new[] { provider },
            () => modelSettingRepo,
            () => detectionRecordRepo);

        var providersList = manager.GetAllProviders();
        _ = Assert.Single(providersList);

        var all = await manager.GetAllAsync();
        _ = Assert.Single(all);

        var info = await manager.GetProviderInfoAsync("testprov");
        Assert.NotNull(info);

        var notFoundInfo = await manager.GetProviderInfoAsync("missing");
        Assert.Null(notFoundInfo);

        var models = await manager.GetModelsAsync("testprov");
        _ = Assert.Single(models);

        var status = await manager.DetectProviderDetailedAsync("testprov");
        Assert.Equal(ProviderStatus.Ready, status.Status);

        var refreshed = await manager.RefreshAllAsync();
        _ = Assert.Single(refreshed);

        Assert.Equal(provider, manager.GetProvider("testprov"));
        _ = Assert.Throws<KeyNotFoundException>(() => manager.GetProvider("missing"));

        await manager.UpdateModelSettingsAsync("testprov", new Dictionary<string, bool> { { "m1", true } });
        _ = await Assert.ThrowsAsync<KeyNotFoundException>(() => manager.UpdateModelSettingsAsync("missing", []));
    }

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

    [Fact]
    public async Task ExecutionOrchestrator_And_PermissionService_Operations_ShouldWork()
    {
        var convRepo = Substitute.For<IConversationRepository>();
        var wsRepo = Substitute.For<IWorkspaceRepository>();
        var providerMgr = Substitute.For<IProviderManager>();
        var broadcaster = Substitute.For<IAgentRealtimeBroadcaster>();
        var snapshotSvc = Substitute.For<ISnapshotService>();
        var permRepo = Substitute.For<IPermissionRequestRepository>();

        var permService = new PermissionService(permRepo, broadcaster);
        var orchestrator = new ExecutionOrchestrator(convRepo, wsRepo, providerMgr, snapshotSvc, broadcaster, permService);

        var ws = Workspace.Create("WS", Path.GetTempPath());
        var conv = Conversation.Create(ws.Id, "Conv Title", "testprovider", "model-1");

        _ = convRepo.GetByIdAsync(conv.Id, Arg.Any<CancellationToken>()).Returns(conv);
        _ = wsRepo.GetByIdAsync(ws.Id, Arg.Any<CancellationToken>()).Returns(ws);

        var provider = Substitute.For<IProvider>();
        _ = provider.Id.Returns("testprovider");
        _ = providerMgr.GetProvider("testprovider").Returns(provider);

        _ = permRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(PermissionRequest.Create(conv.Id, "testprovider", PermissionType.FileWrite, "target", "reason"));

        // Test PermissionService
        var req = await permService.RequestPermissionAsync(conv.Id, "testprovider", PermissionType.FileWrite, "target", "reason");
        Assert.NotNull(req);

        var decided = await permService.DecideAsync(req.Id, true);
        Assert.Equal(PermissionDecision.Approved, decided.Decision);

        _ = convRepo.GetByIdAsync(Arg.Is<Guid>(g => g != conv.Id), Arg.Any<CancellationToken>())
            .Returns((Conversation?)null);

        _ = permRepo.GetByConversationIdAsync(conv.Id, Arg.Any<CancellationToken>())
            .Returns(new List<PermissionRequest> { req });
        var reqList = await permService.GetRequestsByConversationAsync(conv.Id);
        _ = Assert.Single(reqList);

        // Test ExecutionOrchestrator
        _ = provider.ExecuteAsync(Arg.Any<ProviderExecutionContext>()).Returns(Task.CompletedTask);

        await orchestrator.ExecuteAsync(conv.Id, "User Prompt");
        await provider.Received(1).ExecuteAsync(Arg.Is<ProviderExecutionContext>(c => c.Prompt == "User Prompt"));

        // Execution Exception Path
        _ = provider.ExecuteAsync(Arg.Any<ProviderExecutionContext>()).Returns(Task.FromException(new InvalidOperationException("CLI error")));
        await orchestrator.ExecuteAsync(conv.Id, "User Prompt");

        await orchestrator.AbortAsync(conv.Id);
        await provider.Received(1).AbortAsync(conv.Id);

        // Missing Conv / WS exceptions
        _ = await Assert.ThrowsAsync<KeyNotFoundException>(() => orchestrator.ExecuteAsync(Guid.NewGuid(), "prompt"));
    }

    private sealed class TestDatabaseResetter : IDatabaseResetter
    {
        public bool WasWiped { get; private set; }
        public Task WipeAllDataAsync(CancellationToken cancellationToken = default)
        {
            WasWiped = true;
            return Task.CompletedTask;
        }
    }
}
