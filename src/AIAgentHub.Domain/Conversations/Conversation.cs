using AIAgentHub.Domain.Common;
using AIAgentHub.Domain.FileChanges;

namespace AIAgentHub.Domain.Conversations;

public sealed class Conversation : AggregateRoot
{
    public Guid WorkspaceId { get; private set; }
    public string Title { get; private set; } = "New Conversation";
    public string ProviderId { get; private set; } = string.Empty;
    public string? ModelId { get; private set; }
    public string? Effort { get; private set; }
    public string? ProviderSessionId { get; private set; }
    public ConversationStatus Status { get; private set; } = ConversationStatus.Active;
    public bool IsPinned { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastUserInteractionAtUtc { get; private set; } = DateTimeOffset.UtcNow;

    private readonly List<Message> _messages = [];
    public IReadOnlyCollection<Message> Messages => _messages.AsReadOnly();

    private readonly List<FileChange> _fileChanges = [];
    public IReadOnlyCollection<FileChange> FileChanges => _fileChanges.AsReadOnly();

    private readonly List<ConversationProviderSession> _providerSessions = [];
    public IReadOnlyCollection<ConversationProviderSession> ProviderSessions => _providerSessions.AsReadOnly();

    private Conversation() { }

    public static Conversation Create(
        Guid workspaceId,
        string title,
        string providerId,
        string? modelId = null,
        string? providerSessionId = null,
        string? effort = null,
        bool isPinned = false)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("Workspace ID must be valid.", nameof(workspaceId));
        }

        if (string.IsNullOrWhiteSpace(providerId))
        {
            throw new ArgumentException("Provider ID cannot be empty.", nameof(providerId));
        }

        var now = DateTimeOffset.UtcNow;
        return new Conversation
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Title = string.IsNullOrWhiteSpace(title) ? "New Conversation" : title.Trim(),
            ProviderId = providerId.Trim(),
            ModelId = NormalizeModelId(modelId),
            Effort = effort,
            ProviderSessionId = providerSessionId,
            Status = ConversationStatus.Active,
            IsPinned = isPinned,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            LastUserInteractionAtUtc = now
        };
    }

    public void Rename(string newTitle)
    {
        if (string.IsNullOrWhiteSpace(newTitle))
        {
            throw new ArgumentException("Conversation title cannot be empty.", nameof(newTitle));
        }

        Title = newTitle.Trim();
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        LastUserInteractionAtUtc = DateTimeOffset.UtcNow;
    }

    public void SetStatus(ConversationStatus status)
    {
        Status = status;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void SetPinned(bool isPinned)
    {
        IsPinned = isPinned;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void SetProviderAndModel(string providerId, string? modelId, string? effort = null)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            throw new ArgumentException("Provider ID cannot be empty.", nameof(providerId));
        }

        ProviderId = providerId.Trim();
        ModelId = NormalizeModelId(modelId);
        if (effort != null)
        {
            Effort = effort;
        }

        UpdatedAtUtc = DateTimeOffset.UtcNow;
        LastUserInteractionAtUtc = DateTimeOffset.UtcNow;
    }

    private static string? NormalizeModelId(string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId) || modelId.Trim().Equals("default", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return modelId.Trim();
    }

    public void SetEffort(string? effort)
    {
        Effort = effort;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        LastUserInteractionAtUtc = DateTimeOffset.UtcNow;
    }

    public void SetProviderSessionId(string? sessionId)
    {
        ProviderSessionId = sessionId;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public ConversationProviderSession AddOrUpdateProviderSession(string providerId, string? sessionId, Guid? lastSharedMessageId = null, int lastSharedSequenceIndex = 0)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            throw new ArgumentException("Provider ID cannot be empty.", nameof(providerId));
        }

        var normalizedProviderId = providerId.Trim();
        var existing = _providerSessions.FirstOrDefault(s => s.ProviderId.Equals(normalizedProviderId, StringComparison.OrdinalIgnoreCase));
        if (existing == null)
        {
            existing = ConversationProviderSession.Create(Id, normalizedProviderId, sessionId, lastSharedMessageId, lastSharedSequenceIndex);
            _providerSessions.Add(existing);
        }
        else
        {
            existing.UpdateCheckpoint(lastSharedMessageId, lastSharedSequenceIndex, sessionId);
        }

        UpdatedAtUtc = DateTimeOffset.UtcNow;
        return existing;
    }

    public Message AddMessage(
        MessageRole role,
        string content,
        ExecutionMetadata? metadata = null,
        string? originProviderId = null,
        string? originModelId = null)
    {
        var sequenceIndex = _messages.Count + 1;
        var message = Message.Create(
            Id,
            role,
            content,
            metadata,
            sequenceIndex,
            originProviderId ?? ProviderId,
            originModelId ?? ModelId);

        _messages.Add(message);
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        if (role == MessageRole.User)
        {
            LastUserInteractionAtUtc = DateTimeOffset.UtcNow;
        }
        return message;
    }

    public void AddFileChange(FileChange fileChange)
    {
        if (fileChange == null)
        {
            throw new ArgumentNullException(nameof(fileChange));
        }

        _fileChanges.Add(fileChange);
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
