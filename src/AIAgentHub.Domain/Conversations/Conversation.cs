using AIAgentHub.Domain.Common;
using AIAgentHub.Domain.FileChanges;

namespace AIAgentHub.Domain.Conversations;

public sealed class Conversation : AggregateRoot
{
    public Guid WorkspaceId { get; private set; }
    public string Title { get; private set; } = "New Conversation";
    public string ProviderId { get; private set; } = "gemini";
    public string? ModelId { get; private set; }
    public string? Effort { get; private set; }
    public string? ProviderSessionId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;

    private readonly List<Message> _messages = [];
    public IReadOnlyCollection<Message> Messages => _messages.AsReadOnly();

    private readonly List<FileChange> _fileChanges = [];
    public IReadOnlyCollection<FileChange> FileChanges => _fileChanges.AsReadOnly();

    private Conversation() { }

    public static Conversation Create(Guid workspaceId, string title, string providerId = "gemini", string? modelId = null, string? providerSessionId = null, string? effort = null)
    {
        return workspaceId == Guid.Empty
            ? throw new ArgumentException("Workspace ID must be valid.", nameof(workspaceId))
            : new Conversation
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                Title = string.IsNullOrWhiteSpace(title) ? "New Conversation" : title.Trim(),
                ProviderId = string.IsNullOrWhiteSpace(providerId) ? "gemini" : providerId.Trim(),
                ModelId = modelId,
                Effort = effort,
                ProviderSessionId = providerSessionId,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
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
    }

    public void SetProviderAndModel(string providerId, string? modelId, string? effort = null)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            throw new ArgumentException("Provider ID cannot be empty.", nameof(providerId));
        }

        ProviderId = providerId.Trim();
        ModelId = modelId;
        if (effort != null)
        {
            Effort = effort;
        }

        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void SetEffort(string? effort)
    {
        Effort = effort;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void SetProviderSessionId(string? sessionId)
    {
        ProviderSessionId = sessionId;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Message AddMessage(MessageRole role, string content, ExecutionMetadata? metadata = null)
    {
        var message = Message.Create(Id, role, content, metadata);
        _messages.Add(message);
        UpdatedAtUtc = DateTimeOffset.UtcNow;
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
