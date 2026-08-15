using AIAgentHub.Domain.FileChanges;
using AIAgentHub.Domain.Permissions;
using AIAgentHub.Domain.Providers;

namespace AIAgentHub.Application.Realtime;

public interface IAgentRealtimeBroadcaster
{
    public Task SendMessageStreamChunkAsync(Guid conversationId, string chunk, CancellationToken cancellationToken = default);
    public Task SendConversationEventAsync(string eventName, Guid conversationId, object payload, CancellationToken cancellationToken = default);
    public Task SendProviderStatusChangedAsync(string providerId, ProviderStatus status, CancellationToken cancellationToken = default);
    public Task SendDiffCreatedAsync(Guid conversationId, FileChange change, CancellationToken cancellationToken = default);
    public Task SendPermissionRequestedAsync(PermissionRequest request, CancellationToken cancellationToken = default);
    public Task SendNotificationAsync(string level, string title, string message, CancellationToken cancellationToken = default);
}
