using AIAgentHub.Domain.Conversations;

namespace AgentHub.UnitTests.Domain.Conversations;

public sealed class MessageTests
{
    [Fact]
    public void Message_Create_Validations()
    {
        var convId = Guid.NewGuid();
        var meta = new ExecutionMetadata { Action = "test", DurationMs = 100, Tokens = 50, IsSuccess = true };
        var msg = Message.Create(convId, MessageRole.Assistant, "AI Output", meta);

        Assert.NotEqual(Guid.Empty, msg.Id);
        Assert.Equal(convId, msg.ConversationId);
        Assert.Equal(MessageRole.Assistant, msg.Role);
        Assert.Equal("AI Output", msg.Content);
        Assert.Equal(meta, msg.Metadata);

        _ = Assert.Throws<ArgumentException>(() => Message.Create(Guid.Empty, MessageRole.User, "test"));

        var emptyMsg = Message.Create(convId, MessageRole.User, null!);
        Assert.Equal(string.Empty, emptyMsg.Content);
    }

    [Fact]
    public void ExecutionMetadata_PropertySetters()
    {
        var meta = new ExecutionMetadata
        {
            ProviderId = "opencode",
            ModelId = "gpt-5",
            ProviderSessionId = "ses-1",
            Action = "prompt",
            DurationMs = 250,
            Tokens = 120,
            IsSuccess = false,
            ErrorMessage = "Failed execution"
        };

        Assert.Equal("opencode", meta.ProviderId);
        Assert.Equal("gpt-5", meta.ModelId);
        Assert.Equal("ses-1", meta.ProviderSessionId);
        Assert.Equal("prompt", meta.Action);
        Assert.Equal(250, meta.DurationMs);
        Assert.Equal(120, meta.Tokens);
        Assert.False(meta.IsSuccess);
        Assert.Equal("Failed execution", meta.ErrorMessage);
    }
}
