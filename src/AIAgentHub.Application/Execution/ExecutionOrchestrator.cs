using System.Diagnostics;

using AIAgentHub.Application.FileChanges;
using AIAgentHub.Application.Providers;
using AIAgentHub.Application.Realtime;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Domain.Conversations;
using AIAgentHub.Domain.Permissions;
using AIAgentHub.Domain.Repositories;

namespace AIAgentHub.Application.Execution;

public interface IPermissionService
{
    public Task<PermissionRequest> RequestPermissionAsync(Guid conversationId, string providerId, PermissionType type, string target, string reason, CancellationToken cancellationToken = default);
    public Task<PermissionRequest> DecideAsync(Guid requestId, bool approve, CancellationToken cancellationToken = default);
    public Task<IReadOnlyList<PermissionRequest>> GetRequestsByConversationAsync(Guid conversationId, CancellationToken cancellationToken = default);
}

public interface IExecutionOrchestrator
{
    public Task ExecuteAsync(Guid conversationId, string prompt, CancellationToken cancellationToken = default);
    public Task AbortAsync(Guid conversationId);
}

public sealed class PermissionService(IPermissionRequestRepository permissionRepository, IAgentRealtimeBroadcaster broadcaster) : IPermissionService
{
    private readonly IPermissionRequestRepository _permissionRepository = permissionRepository;
    private readonly IAgentRealtimeBroadcaster _broadcaster = broadcaster;

    public async Task<PermissionRequest> RequestPermissionAsync(Guid conversationId, string providerId, PermissionType type, string target, string reason, CancellationToken cancellationToken = default)
    {
        var request = PermissionRequest.Create(conversationId, providerId, type, target, reason);
        await _permissionRepository.AddAsync(request, cancellationToken);
        await _broadcaster.SendPermissionRequestedAsync(request, cancellationToken);
        return request;
    }

    public async Task<PermissionRequest> DecideAsync(Guid requestId, bool approve, CancellationToken cancellationToken = default)
    {
        var req = await _permissionRepository.GetByIdAsync(requestId, cancellationToken) ?? throw new KeyNotFoundException($"Permission request {requestId} not found.");
        req.Decide(approve);
        await _permissionRepository.UpdateAsync(req, cancellationToken);
        return req;
    }

    public Task<IReadOnlyList<PermissionRequest>> GetRequestsByConversationAsync(Guid conversationId, CancellationToken cancellationToken = default) => _permissionRepository.GetByConversationIdAsync(conversationId, cancellationToken);
}

public sealed class ExecutionOrchestrator(
    IConversationRepository conversationRepository,
    IWorkspaceRepository workspaceRepository,
    IProviderManager providerManager,
    ISnapshotService snapshotService,
    IAgentRealtimeBroadcaster broadcaster,
    IPermissionService permissionService,
    CliExecutionOptions? options = null) : IExecutionOrchestrator
{
    private readonly IConversationRepository _conversationRepository = conversationRepository;
    private readonly IWorkspaceRepository _workspaceRepository = workspaceRepository;
    private readonly IProviderManager _providerManager = providerManager;
    private readonly ISnapshotService _snapshotService = snapshotService;
    private readonly IAgentRealtimeBroadcaster _broadcaster = broadcaster;
    private readonly IPermissionService _permissionService = permissionService;
    private readonly CliExecutionOptions? _options = options;

    public async Task ExecuteAsync(Guid conversationId, string prompt, CancellationToken cancellationToken = default)
    {
        var conversation = await _conversationRepository.GetByIdAsync(conversationId, cancellationToken) ?? throw new KeyNotFoundException($"Conversation {conversationId} not found.");

        var workspace = await _workspaceRepository.GetByIdAsync(conversation.WorkspaceId, cancellationToken) ?? throw new KeyNotFoundException($"Workspace {conversation.WorkspaceId} not found.");

        // 1. Add User Message (persisted once for the initiating user prompt)
        var userMsg = conversation.AddMessage(MessageRole.User, prompt, null, conversation.ProviderId, conversation.ModelId);
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

        // Retrieve existing session or start new session
        var currentSession = conversation.ProviderSessions.FirstOrDefault(s => s.ProviderId.Equals(conversation.ProviderId, StringComparison.OrdinalIgnoreCase));
        var activeSessionId = currentSession?.ProviderSessionId ?? conversation.ProviderSessionId;

        if (string.IsNullOrEmpty(activeSessionId))
        {
            activeSessionId = await provider.StartSessionAsync(conversation.Id, workspace.Path, conversation.ModelId, cancellationToken);
            if (!string.IsNullOrEmpty(activeSessionId))
            {
                conversation.SetProviderSessionId(activeSessionId);
                _ = conversation.AddOrUpdateProviderSession(conversation.ProviderId, activeSessionId, userMsg.Id, currentSession?.LastSharedSequenceIndex ?? 0);
                await _conversationRepository.UpdateAsync(conversation, cancellationToken);
            }
        }
        else if (conversation.ProviderSessionId != activeSessionId)
        {
            conversation.SetProviderSessionId(activeSessionId);
            await _conversationRepository.UpdateAsync(conversation, cancellationToken);
        }

        // 3b. Compile Context Handoff / Differential Replay for unshared history
        var lastSharedIndex = currentSession?.LastSharedSequenceIndex ?? 0;
        var unsharedMessages = conversation.Messages
            .Where(m => m.Id != userMsg.Id && m.SequenceIndex > 0 && m.SequenceIndex > lastSharedIndex)
            .OrderBy(m => m.SequenceIndex)
            .ToList();

        var effectivePrompt = prompt;
        if (unsharedMessages.Count > 0)
        {
            var contextBuilder = new System.Text.StringBuilder();
            contextBuilder.AppendLine("[AI Agent Hub — Conversation Context Handoff]");
            contextBuilder.AppendLine("The user switched to you from another AI provider in this ongoing conversation.");
            contextBuilder.AppendLine("Below is the preceding conversation history for context:\n");
            contextBuilder.AppendLine("--- Prior Conversation History ---");
            foreach (var msg in unsharedMessages)
            {
                var roleLabel = msg.Role == MessageRole.User ? "User" : $"Assistant ({msg.OriginProviderId ?? "AI"})";
                contextBuilder.AppendLine($"{roleLabel}: {msg.Content}\n");
            }
            contextBuilder.AppendLine("--- End of Prior History ---\n");
            contextBuilder.AppendLine("Current user message:");
            contextBuilder.AppendLine(prompt);
            effectivePrompt = contextBuilder.ToString();
        }

        var execContext = new ProviderExecutionContext(
            conversation.Id,
            workspace.Id,
            workspace.Path,
            effectivePrompt,
            conversation.ModelId,
            conversation.ProviderSessionId,
            workspace.Settings.IgnoredFiles,
            async (token) =>
            {
                _ = assistantResponseBuilder.Append(token);
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
                    _ = conversation.AddOrUpdateProviderSession(conversation.ProviderId, newSessionId);
                    await _conversationRepository.UpdateAsync(conversation, cancellationToken);
                }
            },
            conversation.Effort,
            OnHeartbeat: async (msg, elapsedSecs) =>
            {
                await _broadcaster.SendConversationEventAsync("conversation.heartbeat", conversationId, new
                {
                    message = msg,
                    elapsedSeconds = elapsedSecs
                }, cancellationToken);
            }
        );

        // 4. Run Execution through Provider (with bounded auto-resumption on timeout)
        var isSuccess = true;
        string? error = null;
        var maxAutoResumes = _options?.AutoResumeOnTimeout == true ? Math.Max(0, _options.MaxAutoResumes) : 0;
        var resumeCount = 0;

        while (true)
        {
            try
            {
                await provider.ExecuteAsync(execContext);
                isSuccess = true;
                error = null;
                break;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                isSuccess = false;
                error = "AI response was cancelled by the user.";
                break;
            }
            catch (TimeoutException tex)
            {
                if (resumeCount < maxAutoResumes && !cancellationToken.IsCancellationRequested)
                {
                    resumeCount++;
                    await _broadcaster.SendNotificationAsync("info", "Auto-Resuming Execution", $"Prompt turn timed out. Automatically continuing from checkpoint (Attempt {resumeCount} of {maxAutoResumes})...", cancellationToken);
                    execContext = execContext with { Prompt = "Continue from where you left off." };
                    continue;
                }

                isSuccess = false;
                error = tex.Message;
                break;
            }
            catch (Exception ex)
            {
                var isTimeout = ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) || assistantResponseBuilder.ToString().Contains("timeout waiting for response", StringComparison.OrdinalIgnoreCase);

                if (isTimeout && resumeCount < maxAutoResumes && !cancellationToken.IsCancellationRequested)
                {
                    resumeCount++;
                    await _broadcaster.SendNotificationAsync("info", "Auto-Resuming Execution", $"Prompt turn timed out. Automatically continuing from checkpoint (Attempt {resumeCount} of {maxAutoResumes})...", cancellationToken);
                    execContext = execContext with { Prompt = "Continue from where you left off." };
                    continue;
                }

                isSuccess = false;
                error = ex.Message;
                await _broadcaster.SendNotificationAsync("error", "AI Execution Failed", ex.Message, cancellationToken);
                break;
            }
        }

        stopwatch.Stop();

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

        var finalAssistantText = assistantResponseBuilder.ToString().Trim();
        if (error == "AI response was cancelled by the user." || (!isSuccess && error != null && error.Contains("cancel", StringComparison.OrdinalIgnoreCase)))
        {
            finalAssistantText = string.IsNullOrWhiteSpace(finalAssistantText)
                ? "*(AI response was cancelled by the user.)*"
                : $"{finalAssistantText}\n\n*(AI response was cancelled by the user.)*";
        }
        else if (string.IsNullOrWhiteSpace(finalAssistantText) && !string.IsNullOrWhiteSpace(error))
        {
            finalAssistantText = $"Error: {error}";
        }

        var assistantMsg = conversation.AddMessage(MessageRole.Assistant, finalAssistantText, metadata, provider.Id, conversation.ModelId);
        _ = conversation.AddOrUpdateProviderSession(conversation.ProviderId, conversation.ProviderSessionId, assistantMsg.Id, assistantMsg.SequenceIndex);
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
            await _broadcaster.SendConversationEventAsync("conversation.aborted", conversationId, new
            {
                message = "AI response was cancelled by the user."
            }, CancellationToken.None);
        }
    }
}
