using AIAgentHub.Application.Realtime;
using AIAgentHub.Domain.FileChanges;
using AIAgentHub.Domain.Permissions;
using AIAgentHub.Domain.Providers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

using AIAgentHub.Infrastructure.Executors;

namespace AIAgentHub.Infrastructure.Realtime;

[Authorize]
public sealed class AgentHubHub(IPtySessionManager ptySessionManager) : Hub
{
    private readonly IPtySessionManager _ptySessionManager = ptySessionManager;

    public async Task JoinConversation(string conversationId) => await Groups.AddToGroupAsync(Context.ConnectionId, $"conv_{conversationId}");

    public async Task LeaveConversation(string conversationId) => await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conv_{conversationId}");

    public async Task SendTerminalInput(string conversationId, string input)
    {
        if (Guid.TryParse(conversationId, out var id))
        {
            await _ptySessionManager.SendInputAsync(id, input);
        }
    }

    public async Task RequestTerminalHistory(string conversationId)
    {
        if (Guid.TryParse(conversationId, out var id))
        {
            var history = _ptySessionManager.GetRecentOutput(id);
            if (!string.IsNullOrEmpty(history))
            {
                await Clients.Caller.SendAsync("terminalOutput", new { conversationId = id, chunk = history });
            }
            var info = _ptySessionManager.GetSessionInfo(id);
            if (info != null)
            {
                await Clients.Caller.SendAsync("terminalMeta", new
                {
                    conversationId = id,
                    thinkingTokens = info.ThinkingTokens,
                    activeBackgroundTask = info.ActiveBackgroundTask,
                    currentTool = info.CurrentTool
                });
            }
        }
    }

    public async Task StartTerminalSession(string conversationId, string workspacePath)
    {
        if (Guid.TryParse(conversationId, out var id))
        {
            _ = await _ptySessionManager.GetOrCreateSessionAsync(id, workspacePath);
        }
    }
}

public sealed class SignalRAgentRealtimeBroadcaster(IHubContext<AgentHubHub> hubContext) : IAgentRealtimeBroadcaster
{
    private readonly IHubContext<AgentHubHub> _hubContext = hubContext;

    public async Task SendMessageStreamChunkAsync(Guid conversationId, string chunk, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group($"conv_{conversationId}").SendAsync("streamChunk", new
        {
            conversationId,
            chunk
        }, cancellationToken);
    }

    public async Task SendConversationEventAsync(string eventName, Guid conversationId, object payload, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.All.SendAsync("conversationEvent", new
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

    public async Task SendDiffCreatedAsync(Guid conversationId, FileChange change, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.All.SendAsync("diffCreated", new
        {
            conversationId,
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
            level,
            title,
            message,
            timestamp = DateTimeOffset.UtcNow
        }, cancellationToken);
    }
}
