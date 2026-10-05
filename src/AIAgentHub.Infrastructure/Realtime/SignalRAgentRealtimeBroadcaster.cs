using System.Security.Claims;
using AIAgentHub.Application.Realtime;
using AIAgentHub.Domain.FileChanges;
using AIAgentHub.Domain.Permissions;
using AIAgentHub.Domain.Providers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AIAgentHub.Infrastructure.Realtime;

[Authorize]
public sealed class AgentHubHub : Hub
{
    private string? GetCurrentUserId()
    {
        return Context.UserIdentifier
            ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.Identity?.Name;
    }

    public async Task JoinConversation(string conversationId)
    {
        if (Guid.TryParse(conversationId, out var convGuid))
        {
            var userId = GetCurrentUserId();
            var userGroup = GetGroupName(userId, convGuid);
            await Groups.AddToGroupAsync(Context.ConnectionId, userGroup);
            await Groups.AddToGroupAsync(Context.ConnectionId, $"conv_{convGuid}");
        }
        else
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"conv_{conversationId.Trim().ToLowerInvariant()}");
        }
    }

    public async Task LeaveConversation(string conversationId)
    {
        if (Guid.TryParse(conversationId, out var convGuid))
        {
            var userId = GetCurrentUserId();
            var userGroup = GetGroupName(userId, convGuid);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, userGroup);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conv_{convGuid}");
        }
        else
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conv_{conversationId.Trim().ToLowerInvariant()}");
        }
    }

    public static string GetGroupName(string? userId, Guid conversationId) =>
        !string.IsNullOrWhiteSpace(userId)
            ? $"usr_{userId.Trim().ToLowerInvariant()}_conv_{conversationId}"
            : $"conv_{conversationId}";
}

public sealed class SignalRAgentRealtimeBroadcaster(IHubContext<AgentHubHub> hubContext) : IAgentRealtimeBroadcaster
{
    private readonly IHubContext<AgentHubHub> _hubContext = hubContext;

    public async Task SendMessageStreamChunkAsync(Guid conversationId, string chunk, string? userId = null, CancellationToken cancellationToken = default)
    {
        var group = AgentHubHub.GetGroupName(userId, conversationId);
        await _hubContext.Clients.Group(group).SendAsync("streamChunk", new
        {
            conversationId,
            chunk
        }, cancellationToken);
    }

    public async Task SendConversationEventAsync(string eventName, Guid conversationId, object payload, string? userId = null, CancellationToken cancellationToken = default)
    {
        var group = AgentHubHub.GetGroupName(userId, conversationId);
        await _hubContext.Clients.Group(group).SendAsync("conversationEvent", new
        {
            eventName,
            conversationId,
            payload
        }, cancellationToken);
    }

    public async Task SendProviderStatusChangedAsync(string providerId, ProviderStatus status, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.All.SendAsync("providerStatusChanged", new
        {
            providerId,
            status = status.ToString()
        }, cancellationToken);
    }

    public async Task SendDiffCreatedAsync(Guid conversationId, FileChange change, string? userId = null, CancellationToken cancellationToken = default)
    {
        var group = AgentHubHub.GetGroupName(userId, conversationId);
        await _hubContext.Clients.Group(group).SendAsync("diffCreated", new
        {
            conversationId,
            fileChangeId = change.Id,
            relativePath = change.RelativePath,
            changeType = change.ChangeType.ToString(),
            status = change.Status.ToString()
        }, cancellationToken);
    }

    public async Task SendPermissionRequestedAsync(PermissionRequest request, string? userId = null, CancellationToken cancellationToken = default)
    {
        var group = AgentHubHub.GetGroupName(userId, request.ConversationId);
        await _hubContext.Clients.Group(group).SendAsync("permissionRequested", new
        {
            id = request.Id,
            conversationId = request.ConversationId,
            providerId = request.ProviderId,
            type = request.Type.ToString(),
            target = request.Target,
            reason = request.Reason
        }, cancellationToken);
    }

    public async Task SendNotificationAsync(string level, string title, string message, string? userId = null, CancellationToken cancellationToken = default)
    {
        var clientTarget = !string.IsNullOrWhiteSpace(userId)
            ? _hubContext.Clients.User(userId)
            : _hubContext.Clients.All;

        await clientTarget.SendAsync("notification", new
        {
            level,
            title,
            message,
            timestamp = DateTimeOffset.UtcNow
        }, cancellationToken);
    }
}
