using AIAgentHub.Application.Conversations;
using AIAgentHub.Domain.Conversations;
using AIAgentHub.Domain.Repositories;
using AIAgentHub.Domain.Workspaces;

using NSubstitute;

namespace AgentHub.UnitTests.Application.Conversations;

public sealed class ConversationServiceTests
{
    [Fact]
    public async Task ConversationService_Operations_ShouldWork()
    {
        var convRepo = Substitute.For<IConversationRepository>();
        var wsRepo = Substitute.For<IWorkspaceRepository>();
        var service = new ConversationService(convRepo, wsRepo);

        var wsId = Guid.NewGuid();
        var ws = Workspace.Create("WS", Path.GetTempPath(), WorkspaceOrigin.Server, new WorkspaceSettings { DefaultProviderId = "antigravity" });
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

        _ = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.CreateAsync(new CreateConversationRequest(Guid.NewGuid(), "Title", "antigravity")));

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
    public async Task ConversationService_CreateAsync_NoProviderAvailable_ThrowsInvalidOperationException()
    {
        var convRepo = Substitute.For<IConversationRepository>();
        var wsRepo = Substitute.For<IWorkspaceRepository>();
        var service = new ConversationService(convRepo, wsRepo);

        var wsId = Guid.NewGuid();
        var ws = Workspace.Create("WS", Path.GetTempPath(), WorkspaceOrigin.Server, new WorkspaceSettings { DefaultProviderId = null });
        _ = wsRepo.GetByIdAsync(wsId, Arg.Any<CancellationToken>()).Returns(ws);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(new CreateConversationRequest(wsId, "Conv Without Provider", ProviderId: null)));
    }

    [Fact]
    public async Task ConversationService_GetByWorkspaceIdAsync_OrdersByLastUserInteractionAtUtc_Descending()
    {
        var convRepo = Substitute.For<IConversationRepository>();
        var wsRepo = Substitute.For<IWorkspaceRepository>();
        var service = new ConversationService(convRepo, wsRepo);

        var wsId = Guid.NewGuid();
        var conv1 = Conversation.Create(wsId, "Older Conversation", "antigravity");
        await Task.Delay(10);
        var conv2 = Conversation.Create(wsId, "Newer Conversation", "antigravity");

        // Older conversation has a recent user message
        await Task.Delay(10);
        _ = conv1.AddMessage(MessageRole.User, "Recent query on older conv");

        // Newer conversation has an assistant message (which should not make it more recent than user interaction)
        await Task.Delay(10);
        _ = conv2.AddMessage(MessageRole.Assistant, "AI answer");

        _ = convRepo.GetByWorkspaceIdAsync(wsId, Arg.Any<CancellationToken>())
            .Returns(new List<Conversation> { conv2, conv1 });

        var list = await service.GetByWorkspaceIdAsync(wsId);

        Assert.Equal(2, list.Count);
        Assert.Equal("Older Conversation", list[0].Title);
        Assert.Equal("Newer Conversation", list[1].Title);
    }
}
