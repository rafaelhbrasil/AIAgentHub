using AIAgentHub.Application.Realtime;
using AIAgentHub.Domain.FileChanges;
using AIAgentHub.Domain.Permissions;
using AIAgentHub.Domain.Providers;
using Microsoft.AspNetCore.SignalR;

namespace AIAgentHub.Infrastructure.Realtime;

public sealed class AgentHubHub : Hub
{
    public async Task JoinConversation(string conversationId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"conv_{conversationId}");
    }

    public async Task LeaveConversation(string conversationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conv_{conversationId}");
    }
}

public sealed class SignalRAgentRealtimeBroadcaster : IAgentRealtimeBroadcaster
{
    private readonly IHubContext<AgentHubHub> _hubContext;

    public SignalRAgentRealtimeBroadcaster(IHubContext<AgentHubHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task SendMessageStreamChunkAsync(Guid conversationId, string chunk, CancellationToken cancellationToken = default)
    {
        // Broadcast to both the specific conversation channel and connected active sessions
        await _hubContext.Clients.Group($"conv_{conversationId}").SendAsync("streamChunk", new
        {
            conversationId = conversationId,
            chunk = chunk
        }, cancellationToken);

        await _hubContext.Clients.All.SendAsync("streamChunk", new
        {
            conversationId = conversationId,
            chunk = chunk
        }, cancellationToken);
    }

    public async Task SendConversationEventAsync(string eventName, Guid conversationId, object payload, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.All.SendAsync("conversationEvent", new
        {
            eventName = eventName,
            conversationId = conversationId,
            payload = payload
        }, cancellationToken);
    }

    public async Task SendProviderStatusChangedAsync(string providerId, ProviderStatus status, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.All.SendAsync("providerStatusChanged", new
        {
            providerId = providerId,
            status = status.ToString()
        }, cancellationToken);
    }

    public async Task SendDiffCreatedAsync(Guid conversationId, FileChange change, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.All.SendAsync("diffCreated", new
        {
            conversationId = conversationId,
            fileChangeId = change.Id,
            relativePath = change.RelativePath,
            changeType = change.ChangeType.ToString(),
            status = change.Status.ToString()
        }, cancellationToken);
    }

    public async Task SendPermissionRequestedAsync(PermissionRequest request, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.All.SendAsync("permissionRequested", new
        {
            id = request.Id,
            conversationId = request.ConversationId,
            providerId = request.ProviderId,
            type = request.Type.ToString(),
            target = request.Target,
            reason = request.Reason
        }, cancellationToken);
    }

    public async Task SendNotificationAsync(string level, string title, string message, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.All.SendAsync("notification", new
        {
            level = level,
            title = title,
            message = message,
            timestamp = DateTimeOffset.UtcNow
        }, cancellationToken);
    }
}
