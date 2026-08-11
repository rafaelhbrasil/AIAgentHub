using AIAgentHub.Domain.Common;

namespace AIAgentHub.Domain.Conversations;

public sealed class Message : Entity
{
    public Guid ConversationId { get; private set; }
    public MessageRole Role { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;
    public ExecutionMetadata? Metadata { get; private set; }

    private Message() { }

    public static Message Create(Guid conversationId, MessageRole role, string content, ExecutionMetadata? metadata = null)
    {
        if (conversationId == Guid.Empty)
            throw new ArgumentException("Conversation ID must be valid.", nameof(conversationId));

        return new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Role = role,
            Content = content ?? string.Empty,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Metadata = metadata
        };
    }
}
