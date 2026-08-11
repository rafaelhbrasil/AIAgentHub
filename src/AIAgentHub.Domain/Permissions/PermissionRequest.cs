using AIAgentHub.Domain.Common;

namespace AIAgentHub.Domain.Permissions;

public enum PermissionType
{
    FileRead = 0,
    FileWrite = 1,
    FileDelete = 2,
    CommandExecution = 3,
    DirectoryAccess = 4
}

public enum PermissionDecision
{
    Pending = 0,
    Approved = 1,
    Denied = 2
}

public sealed class PermissionRequest : Entity
{
    public Guid ConversationId { get; private set; }
    public string ProviderId { get; private set; } = string.Empty;
    public PermissionType Type { get; private set; }
    public string Target { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public PermissionDecision Decision { get; private set; } = PermissionDecision.Pending;
    public DateTimeOffset RequestedAtUtc { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DecidedAtUtc { get; private set; }

    private PermissionRequest() { }

    public static PermissionRequest Create(Guid conversationId, string providerId, PermissionType type, string target, string reason)
    {
        return new PermissionRequest
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            ProviderId = providerId ?? string.Empty,
            Type = type,
            Target = target ?? string.Empty,
            Reason = reason ?? string.Empty,
            Decision = PermissionDecision.Pending,
            RequestedAtUtc = DateTimeOffset.UtcNow
        };
    }

    public void Decide(bool approve)
    {
        Decision = approve ? PermissionDecision.Approved : PermissionDecision.Denied;
        DecidedAtUtc = DateTimeOffset.UtcNow;
    }
}
