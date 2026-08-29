using AIAgentHub.Domain.Common;

namespace AIAgentHub.Domain.Conversations;

public sealed class Message : Entity
{
    public Guid ConversationId { get; private set; }
    public MessageRole Role { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public int SequenceIndex { get; private set; }
    public string? OriginProviderId { get; private set; }
    public string? OriginModelId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;
    public ExecutionMetadata? Metadata { get; private set; }

    private Message() { }

    public static Message Create(
        Guid conversationId,
        MessageRole role,
        string content,
        ExecutionMetadata? metadata = null,
        int sequenceIndex = 0,
        string? originProviderId = null,
        string? originModelId = null)
    {
        return conversationId == Guid.Empty
            ? throw new ArgumentException("Conversation ID must be valid.", nameof(conversationId))
            : new Message
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                Role = role,
                Content = content ?? string.Empty,
                SequenceIndex = sequenceIndex,
                OriginProviderId = originProviderId,
                OriginModelId = originModelId,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                Metadata = metadata
            };
    }

    public void SetSequenceIndex(int index)
    {
        SequenceIndex = index;
    }

    public void SetOrigin(string? providerId, string? modelId)
    {
        OriginProviderId = providerId;
        OriginModelId = modelId;
    }
}
