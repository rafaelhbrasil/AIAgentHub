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
}
