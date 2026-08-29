using AIAgentHub.Domain.Common;

namespace AIAgentHub.Domain.Conversations;

public sealed class ConversationProviderSession : Entity
{
    public Guid ConversationId { get; private set; }
    public string ProviderId { get; private set; } = string.Empty;
    public string? ProviderSessionId { get; private set; }
    public Guid? LastSharedMessageId { get; private set; }
    public int LastSharedSequenceIndex { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastActiveAtUtc { get; private set; } = DateTimeOffset.UtcNow;

    private ConversationProviderSession() { }

    public static ConversationProviderSession Create(Guid conversationId, string providerId, string? providerSessionId = null, Guid? lastSharedMessageId = null, int lastSharedSequenceIndex = 0)
    {
        if (conversationId == Guid.Empty)
        {
            throw new ArgumentException("Conversation ID must be valid.", nameof(conversationId));
        }

        if (string.IsNullOrWhiteSpace(providerId))
        {
            throw new ArgumentException("Provider ID cannot be empty.", nameof(providerId));
        }

        var now = DateTimeOffset.UtcNow;
        return new ConversationProviderSession
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            ProviderId = providerId.Trim(),
            ProviderSessionId = providerSessionId?.Trim(),
            LastSharedMessageId = lastSharedMessageId,
            LastSharedSequenceIndex = lastSharedSequenceIndex,
            CreatedAtUtc = now,
            LastActiveAtUtc = now
        };
    }

    public void UpdateCheckpoint(Guid? lastSharedMessageId, int lastSharedSequenceIndex, string? providerSessionId = null)
    {
        LastSharedMessageId = lastSharedMessageId;
        LastSharedSequenceIndex = lastSharedSequenceIndex;
        if (!string.IsNullOrWhiteSpace(providerSessionId))
        {
            ProviderSessionId = providerSessionId.Trim();
        }
        LastActiveAtUtc = DateTimeOffset.UtcNow;
    }

    public void Touch()
    {
        LastActiveAtUtc = DateTimeOffset.UtcNow;
    }
}
