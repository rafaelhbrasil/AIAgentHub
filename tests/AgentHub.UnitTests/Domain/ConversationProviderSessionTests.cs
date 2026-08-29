using AIAgentHub.Domain.Conversations;
using AIAgentHub.Domain.Workspaces;
using Xunit;

namespace AgentHub.UnitTests.Domain;

public class ConversationProviderSessionTests
{
    [Fact]
    public void Create_ValidParameters_InstantiatesSession()
    {
        var convId = Guid.NewGuid();
        var session = ConversationProviderSession.Create(convId, "claude-code", "session-123");

        Assert.Equal(convId, session.ConversationId);
        Assert.Equal("claude-code", session.ProviderId);
        Assert.Equal("session-123", session.ProviderSessionId);
        Assert.Equal(0, session.LastSharedSequenceIndex);
        Assert.Null(session.LastSharedMessageId);
    }

    [Fact]
    public void UpdateCheckpoint_UpdatesLastSharedValues()
    {
        var session = ConversationProviderSession.Create(Guid.NewGuid(), "gemini");
        var msgId = Guid.NewGuid();
        session.UpdateCheckpoint(msgId, 5, "gemini-session-99");

        Assert.Equal(msgId, session.LastSharedMessageId);
        Assert.Equal(5, session.LastSharedSequenceIndex);
        Assert.Equal("gemini-session-99", session.ProviderSessionId);
    }

    [Fact]
    public void Conversation_PinAndStatus_WorksCorrectly()
    {
        var conv = Conversation.Create(Guid.NewGuid(), "Test Conversation", "antigravity");
        Assert.False(conv.IsPinned);
        Assert.Equal(ConversationStatus.Active, conv.Status);

        conv.SetPinned(true);
        Assert.True(conv.IsPinned);

        conv.SetStatus(ConversationStatus.SwitchingProvider);
        Assert.Equal(ConversationStatus.SwitchingProvider, conv.Status);
    }

    [Fact]
    public void Conversation_AddMessage_SetsSequenceIndexAndAttribution()
    {
        var conv = Conversation.Create(Guid.NewGuid(), "Test Conversation", "claude-code");
        var msg1 = conv.AddMessage(MessageRole.User, "Hello");
        var msg2 = conv.AddMessage(MessageRole.Assistant, "Hi there!", null, "claude-code", "claude-3-7-sonnet");

        Assert.Equal(1, msg1.SequenceIndex);
        Assert.Equal(2, msg2.SequenceIndex);
        Assert.Equal("claude-code", msg2.OriginProviderId);
        Assert.Equal("claude-3-7-sonnet", msg2.OriginModelId);
    }

    [Fact]
    public void Workspace_FavoriteAndArchive_WorksCorrectly()
    {
        var ws = Workspace.Create("Test Workspace", "D:\\Test\\Path");
        Assert.False(ws.IsFavorite);
        Assert.False(ws.IsArchived);

        ws.SetFavorite(true);
        Assert.True(ws.IsFavorite);

        ws.SetArchived(true);
        Assert.True(ws.IsArchived);
    }
}
