using AIAgentHub.Domain.FileChanges;
using AIAgentHub.Domain.Permissions;
using AIAgentHub.Domain.Providers;

namespace AIAgentHub.Application.Realtime;

public interface IAgentRealtimeBroadcaster
{
    public Task SendMessageStreamChunkAsync(Guid conversationId, string chunk, string? userId = null, CancellationToken cancellationToken = default);
    public Task SendConversationEventAsync(string eventName, Guid conversationId, object payload, string? userId = null, CancellationToken cancellationToken = default);
    public Task SendProviderStatusChangedAsync(string providerId, ProviderStatus status, CancellationToken cancellationToken = default);
    public Task SendDiffCreatedAsync(Guid conversationId, FileChange change, string? userId = null, CancellationToken cancellationToken = default);
    public Task SendPermissionRequestedAsync(PermissionRequest request, string? userId = null, CancellationToken cancellationToken = default);
    public Task SendNotificationAsync(string level, string title, string message, string? userId = null, CancellationToken cancellationToken = default);
}
