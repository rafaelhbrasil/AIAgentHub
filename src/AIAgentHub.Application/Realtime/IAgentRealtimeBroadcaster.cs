using AIAgentHub.Domain.FileChanges;
using AIAgentHub.Domain.Permissions;
using AIAgentHub.Domain.Providers;

namespace AIAgentHub.Application.Realtime;

public interface IAgentRealtimeBroadcaster
{
    Task SendMessageStreamChunkAsync(Guid conversationId, string chunk, CancellationToken cancellationToken = default);
    Task SendConversationEventAsync(string eventName, Guid conversationId, object payload, CancellationToken cancellationToken = default);
    Task SendProviderStatusChangedAsync(string providerId, ProviderStatus status, CancellationToken cancellationToken = default);
    Task SendDiffCreatedAsync(Guid conversationId, FileChange change, CancellationToken cancellationToken = default);
    Task SendPermissionRequestedAsync(PermissionRequest request, CancellationToken cancellationToken = default);
    Task SendNotificationAsync(string level, string title, string message, CancellationToken cancellationToken = default);
}
