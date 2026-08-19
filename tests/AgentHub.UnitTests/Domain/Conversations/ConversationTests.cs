using AIAgentHub.Domain.Conversations;
using AIAgentHub.Domain.FileChanges;

namespace AgentHub.UnitTests.Domain.Conversations;

public sealed class ConversationTests
{
    [Fact]
    public void Conversation_Create_And_AddMessage_ShouldWork()
    {
        var wsId = Guid.NewGuid();
        var conv = Conversation.Create(wsId, "Initial Task", "gemini", "gemini-2.5-pro", "session-1", "high");

        Assert.Equal("Initial Task", conv.Title);
        Assert.Equal("gemini", conv.ProviderId);
        Assert.Equal("gemini-2.5-pro", conv.ModelId);
        Assert.Equal("session-1", conv.ProviderSessionId);
        Assert.Equal("high", conv.Effort);

        var msg = conv.AddMessage(MessageRole.User, "Hello AI");
        _ = Assert.Single(conv.Messages);
        Assert.Equal(MessageRole.User, msg.Role);
        Assert.Equal("Hello AI", msg.Content);
    }

    [Fact]
    public void Conversation_Create_InvalidWorkspace_ShouldThrow() => _ = Assert.Throws<ArgumentException>(() => Conversation.Create(Guid.Empty, "Title"));

    [Fact]
    public void Conversation_Create_DefaultTitleAndProvider_Fallback()
    {
        var conv = Conversation.Create(Guid.NewGuid(), "", "  ");
        Assert.Equal("New Conversation", conv.Title);
        Assert.Equal("gemini", conv.ProviderId);
    }

    [Fact]
    public void Conversation_Rename_SetProviderModel_SetEffort_SetSessionId()
    {
        var conv = Conversation.Create(Guid.NewGuid(), "Title");

        conv.Rename("Updated Title");
        Assert.Equal("Updated Title", conv.Title);
        _ = Assert.Throws<ArgumentException>(() => conv.Rename(""));

        conv.SetProviderAndModel("claude", "claude-3-7-sonnet", "medium");
        Assert.Equal("claude", conv.ProviderId);
        Assert.Equal("claude-3-7-sonnet", conv.ModelId);
        Assert.Equal("medium", conv.Effort);
        _ = Assert.Throws<ArgumentException>(() => conv.SetProviderAndModel("", "model"));

        conv.SetEffort("low");
        Assert.Equal("low", conv.Effort);

        conv.SetProviderSessionId("ses-123");
        Assert.Equal("ses-123", conv.ProviderSessionId);
    }

    [Fact]
    public void Conversation_AddFileChange_ShouldAppendAndTouch()
    {
        var conv = Conversation.Create(Guid.NewGuid(), "Title");
        var change = FileChange.Create(conv.Id, "src/index.js", FileChangeType.Created);

        conv.AddFileChange(change);
        _ = Assert.Single(conv.FileChanges);
        _ = Assert.Throws<ArgumentNullException>(() => conv.AddFileChange(null!));
    }

    [Fact]
    public async Task Conversation_LastUserInteractionAtUtc_ShouldUpdateOnUserMessageOnly()
    {
        var conv = Conversation.Create(Guid.NewGuid(), "Title");
        var initialInteraction = conv.LastUserInteractionAtUtc;

        await Task.Delay(10);
        _ = conv.AddMessage(MessageRole.User, "User query");
        var afterUserMessage = conv.LastUserInteractionAtUtc;
        Assert.True(afterUserMessage > initialInteraction);

        await Task.Delay(10);
        _ = conv.AddMessage(MessageRole.Assistant, "Assistant response");
        var afterAssistantMessage = conv.LastUserInteractionAtUtc;
        Assert.Equal(afterUserMessage, afterAssistantMessage);
        Assert.True(conv.UpdatedAtUtc > afterUserMessage);
    }
}
