using System.Diagnostics;
using AIAgentHub.Application.Conversations;
using AIAgentHub.Application.FileChanges;
using AIAgentHub.Application.Providers;
using AIAgentHub.Application.Realtime;
using AIAgentHub.Domain.Conversations;
using AIAgentHub.Domain.Permissions;
using AIAgentHub.Domain.Repositories;

namespace AIAgentHub.Application.Execution;

public interface IPermissionService
{
    Task<PermissionRequest> RequestPermissionAsync(Guid conversationId, string providerId, PermissionType type, string target, string reason, CancellationToken cancellationToken = default);
    Task<PermissionRequest> DecideAsync(Guid requestId, bool approve, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PermissionRequest>> GetRequestsByConversationAsync(Guid conversationId, CancellationToken cancellationToken = default);
}

public interface IExecutionOrchestrator
{
    Task ExecuteAsync(Guid conversationId, string prompt, CancellationToken cancellationToken = default);
    Task AbortAsync(Guid conversationId);
}

public sealed class PermissionService : IPermissionService
{
    private readonly IPermissionRequestRepository _permissionRepository;
    private readonly IAgentRealtimeBroadcaster _broadcaster;

    public PermissionService(IPermissionRequestRepository permissionRepository, IAgentRealtimeBroadcaster broadcaster)
    {
        _permissionRepository = permissionRepository;
        _broadcaster = broadcaster;
    }

    public async Task<PermissionRequest> RequestPermissionAsync(Guid conversationId, string providerId, PermissionType type, string target, string reason, CancellationToken cancellationToken = default)
    {
        var request = PermissionRequest.Create(conversationId, providerId, type, target, reason);
        await _permissionRepository.AddAsync(request, cancellationToken);
        await _broadcaster.SendPermissionRequestedAsync(request, cancellationToken);
        return request;
    }

    public async Task<PermissionRequest> DecideAsync(Guid requestId, bool approve, CancellationToken cancellationToken = default)
    {
        var req = await _permissionRepository.GetByIdAsync(requestId, cancellationToken);
        if (req == null)
            throw new KeyNotFoundException($"Permission request {requestId} not found.");

        req.Decide(approve);
        await _permissionRepository.UpdateAsync(req, cancellationToken);
        return req;
    }

    public Task<IReadOnlyList<PermissionRequest>> GetRequestsByConversationAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        return _permissionRepository.GetByConversationIdAsync(conversationId, cancellationToken);
    }
}

public sealed class ExecutionOrchestrator : IExecutionOrchestrator
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IProviderManager _providerManager;
    private readonly ISnapshotService _snapshotService;
    private readonly IAgentRealtimeBroadcaster _broadcaster;
    private readonly IPermissionService _permissionService;

    public ExecutionOrchestrator(
        IConversationRepository conversationRepository,
        IWorkspaceRepository workspaceRepository,
        IProviderManager providerManager,
        ISnapshotService snapshotService,
        IAgentRealtimeBroadcaster broadcaster,
        IPermissionService permissionService)
    {
        _conversationRepository = conversationRepository;
        _workspaceRepository = workspaceRepository;
        _providerManager = providerManager;
        _snapshotService = snapshotService;
        _broadcaster = broadcaster;
        _permissionService = permissionService;
    }

    public async Task ExecuteAsync(Guid conversationId, string prompt, CancellationToken cancellationToken = default)
    {
        var conversation = await _conversationRepository.GetByIdAsync(conversationId, cancellationToken);
        if (conversation == null)
            throw new KeyNotFoundException($"Conversation {conversationId} not found.");

        var workspace = await _workspaceRepository.GetByIdAsync(conversation.WorkspaceId, cancellationToken);
        if (workspace == null)
            throw new KeyNotFoundException($"Workspace {conversation.WorkspaceId} not found.");

        // 1. Add User Message
        conversation.AddMessage(MessageRole.User, prompt);
        await _conversationRepository.UpdateAsync(conversation, cancellationToken);
        await _broadcaster.SendConversationEventAsync("conversation.started", conversationId, new { Prompt = prompt }, cancellationToken);

        // 2. Pre-execution Snapshot for change detection and atomic rollback
        var snapshotToken = await _snapshotService.CaptureWorkspaceSnapshotAsync(
            workspace.Id,
            conversation.Id,
            workspace.Path,
            workspace.Settings.IgnoredFiles,
            cancellationToken);

        // 3. Prepare Provider Execution Context
        var provider = _providerManager.GetProvider(conversation.ProviderId);
        var stopwatch = Stopwatch.StartNew();
        var assistantResponseBuilder = new System.Text.StringBuilder();

        // Start session if not already started
        if (string.IsNullOrEmpty(conversation.ProviderSessionId))
        {
            var sessionId = await provider.StartSessionAsync(conversation.Id, workspace.Path, conversation.ModelId, cancellationToken);
            if (!string.IsNullOrEmpty(sessionId))
            {
                conversation.SetProviderSessionId(sessionId);
                await _conversationRepository.UpdateAsync(conversation, cancellationToken);
            }
        }

        var execContext = new ProviderExecutionContext(
            conversation.Id,
            workspace.Id,
            workspace.Path,
            prompt,
            conversation.ModelId,
            conversation.ProviderSessionId,
            workspace.Settings.IgnoredFiles,
            async (token) =>
            {
                assistantResponseBuilder.Append(token);
                await _broadcaster.SendMessageStreamChunkAsync(conversationId, token, cancellationToken);
            },
            async (actionType, target) =>
            {
                var permType = actionType.ToLowerInvariant() switch
                {
                    "file_write" => PermissionType.FileWrite,
                    "file_delete" => PermissionType.FileDelete,
                    "command" => PermissionType.CommandExecution,
                    _ => PermissionType.DirectoryAccess
                };
                var req = await _permissionService.RequestPermissionAsync(conversationId, provider.Id, permType, target, $"AI assistant requested {actionType} for {target}", cancellationToken);
                return req.Decision == PermissionDecision.Approved;
            },
            cancellationToken,
            async (newSessionId) =>
            {
                if (!string.IsNullOrEmpty(newSessionId) && conversation.ProviderSessionId != newSessionId)
                {
                    conversation.SetProviderSessionId(newSessionId);
                    await _conversationRepository.UpdateAsync(conversation, cancellationToken);
                }
            },
            conversation.Effort
        );

        // 4. Run Execution through Provider
        bool isSuccess = true;
        string? error = null;

        try
        {
            await provider.ExecuteAsync(execContext);
        }
        catch (Exception ex)
        {
            isSuccess = false;
            error = ex.Message;
            await _broadcaster.SendNotificationAsync("error", "AI Execution Failed", ex.Message, cancellationToken);
        }
        finally
        {
            stopwatch.Stop();
        }

        // 5. Post-execution Snapshot Comparison & Diff Generation
        var detectedChanges = await _snapshotService.DetectAndRecordChangesAsync(
            workspace.Id,
            conversation.Id,
            workspace.Path,
            snapshotToken,
            workspace.Settings.IgnoredFiles,
            cancellationToken);

        foreach (var change in detectedChanges)
        {
            conversation.AddFileChange(change);
            await _broadcaster.SendDiffCreatedAsync(conversationId, change, cancellationToken);
        }

        // 6. Record Assistant Message in History
        var metadata = new ExecutionMetadata
        {
            ProviderId = provider.Id,
            ModelId = conversation.ModelId,
            ProviderSessionId = conversation.ProviderSessionId,
            Timestamp = DateTimeOffset.UtcNow,
            DurationMs = stopwatch.ElapsedMilliseconds,
            IsSuccess = isSuccess,
            ErrorMessage = error
        };

        var finalAssistantText = assistantResponseBuilder.ToString();
        if (string.IsNullOrWhiteSpace(finalAssistantText) && !string.IsNullOrWhiteSpace(error))
        {
            finalAssistantText = $"Error: {error}";
        }

        conversation.AddMessage(MessageRole.Assistant, finalAssistantText, metadata);
        await _conversationRepository.UpdateAsync(conversation, cancellationToken);

        await _broadcaster.SendConversationEventAsync("conversation.completed", conversationId, new
        {
            DurationMs = stopwatch.ElapsedMilliseconds,
            FileChangesCount = detectedChanges.Count,
            IsSuccess = isSuccess
        }, cancellationToken);
    }

    public async Task AbortAsync(Guid conversationId)
    {
        var conversation = await _conversationRepository.GetByIdAsync(conversationId);
        if (conversation != null)
        {
            var provider = _providerManager.GetProvider(conversation.ProviderId);
            await provider.AbortAsync(conversationId);
            await _broadcaster.SendConversationEventAsync("conversation.aborted", conversationId, new { }, CancellationToken.None);
        }
    }
}
