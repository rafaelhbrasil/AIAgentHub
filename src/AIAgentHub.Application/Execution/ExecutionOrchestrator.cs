using System.Diagnostics;

using AIAgentHub.Application.FileChanges;
using AIAgentHub.Application.Providers;
using AIAgentHub.Application.Realtime;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Domain.Conversations;
using AIAgentHub.Domain.FileChanges;
using AIAgentHub.Domain.Permissions;
using AIAgentHub.Domain.Repositories;
using AIAgentHub.Domain.Workspaces;

namespace AIAgentHub.Application.Execution;

public interface IPermissionService
{
    public Task<PermissionRequest> RequestPermissionAsync(Guid conversationId, string providerId, PermissionType type, string target, string reason, string? userId = null, CancellationToken cancellationToken = default);
    public Task<PermissionRequest> DecideAsync(Guid requestId, bool approve, CancellationToken cancellationToken = default);
    public Task<IReadOnlyList<PermissionRequest>> GetRequestsByConversationAsync(Guid conversationId, CancellationToken cancellationToken = default);
}

public interface IExecutionOrchestrator
{
    public Task ExecuteAsync(Guid conversationId, string prompt, string? userId = null, CancellationToken cancellationToken = default);
    public Task AbortAsync(Guid conversationId, string? userId = null);
    public bool IsExecuting(Guid conversationId);
}

public sealed class PermissionService(IPermissionRequestRepository permissionRepository, IAgentRealtimeBroadcaster broadcaster) : IPermissionService
{
    private readonly IPermissionRequestRepository _permissionRepository = permissionRepository;
    private readonly IAgentRealtimeBroadcaster _broadcaster = broadcaster;

    public async Task<PermissionRequest> RequestPermissionAsync(Guid conversationId, string providerId, PermissionType type, string target, string reason, string? userId = null, CancellationToken cancellationToken = default)
    {
        var request = PermissionRequest.Create(conversationId, providerId, type, target, reason);
        await _permissionRepository.AddAsync(request, cancellationToken);
        await _broadcaster.SendPermissionRequestedAsync(request, userId, cancellationToken);
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
    IActiveExecutionTracker activeExecutionTracker,
    CliExecutionOptions options,
    IPromptLogger promptLogger) : IExecutionOrchestrator
{
    private readonly IConversationRepository _conversationRepository = conversationRepository;
    private readonly IWorkspaceRepository _workspaceRepository = workspaceRepository;
    private readonly IProviderManager _providerManager = providerManager;
    private readonly ISnapshotService _snapshotService = snapshotService;
    private readonly IAgentRealtimeBroadcaster _broadcaster = broadcaster;
    private readonly IPermissionService _permissionService = permissionService;
    private readonly IActiveExecutionTracker _activeExecutionTracker = activeExecutionTracker;
    private readonly CliExecutionOptions _options = options;
    private readonly IPromptLogger _promptLogger = promptLogger;

    public bool IsExecuting(Guid conversationId) => _activeExecutionTracker.IsExecuting(conversationId);

    public async Task ExecuteAsync(Guid conversationId, string prompt, string? userId = null, CancellationToken cancellationToken = default)
    {
        if (!_activeExecutionTracker.TryGetCancellationTokenSource(conversationId, out var activeCts) || activeCts == null)
        {
            var localCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (!_activeExecutionTracker.TryStart(conversationId, localCts))
            {
                localCts.Dispose();
                throw new InvalidOperationException($"An execution is already running for conversation {conversationId}.");
            }
            activeCts = localCts;
        }

        try
        {
            var token = activeCts.Token;
            var conversation = await _conversationRepository.GetByIdAsync(conversationId, token) ?? throw new KeyNotFoundException($"Conversation {conversationId} not found.");

            var workspace = await _workspaceRepository.GetByIdAsync(conversation.WorkspaceId, token) ?? throw new KeyNotFoundException($"Workspace {conversation.WorkspaceId} not found.");

            // 1. Add User Message (persisted once for the initiating user prompt)
            var userMsg = conversation.AddMessage(MessageRole.User, prompt, null, conversation.ProviderId, conversation.ModelId);
            await _conversationRepository.UpdateAsync(conversation, token);
            await _broadcaster.SendConversationEventAsync("conversation.started", conversationId, new { Prompt = prompt }, userId, token);

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

            await StartOrResumeProviderSessionAsync(conversation, provider, workspace.Path, userMsg.Id, cancellationToken);

            var effectivePrompt = BuildEffectivePrompt(conversation, userMsg.Id, prompt);

            var promptContext = new ExecutionPromptContext(effectivePrompt, assistantResponseBuilder, userMsg.Id, userId);
            var execContext = BuildExecutionContext(conversation, workspace, provider, promptContext, token);

            var (isSuccess, error, _) = await RunWithAutoResumeAsync(provider, execContext, assistantResponseBuilder, userId, token);

            stopwatch.Stop();

            IReadOnlyList<FileChange> detectedChanges = [];
            try
            {
                detectedChanges = await _snapshotService.DetectAndRecordChangesAsync(
                    workspace.Id,
                    conversation.Id,
                    workspace.Path,
                    snapshotToken,
                    workspace.Settings.IgnoredFiles,
                    CancellationToken.None);

                foreach (var change in detectedChanges)
                {
                    conversation.AddFileChange(change);
                    await _broadcaster.SendDiffCreatedAsync(conversationId, change, userId, CancellationToken.None);
                }
            }
            catch
            {
                // Snapshot comparison failure should not block recording assistant message or session checkpoint
            }

            var executionResult = new AssistantExecutionResult(isSuccess, error, stopwatch.ElapsedMilliseconds, detectedChanges.Count);
            await RecordAssistantResponseAsync(conversation, provider, assistantResponseBuilder, executionResult, userId);
        }
        finally
        {
            _ = _activeExecutionTracker.TryStop(conversationId);
        }
    }

    private async Task StartOrResumeProviderSessionAsync(
        Conversation conversation,
        IProvider provider,
        string workspacePath,
        Guid userMsgId,
        CancellationToken cancellationToken)
    {
        var currentSession = conversation.ProviderSessions.FirstOrDefault(s => s.ProviderId.Equals(conversation.ProviderId, StringComparison.OrdinalIgnoreCase));
        var activeSessionId = currentSession?.ProviderSessionId ?? conversation.ProviderSessionId;

        if (string.IsNullOrEmpty(activeSessionId))
        {
            activeSessionId = await provider.StartSessionAsync(conversation.Id, workspacePath, conversation.ModelId, cancellationToken);
            if (!string.IsNullOrEmpty(activeSessionId))
            {
                conversation.SetProviderSessionId(activeSessionId);
                _ = conversation.AddOrUpdateProviderSession(conversation.ProviderId, activeSessionId, userMsgId, currentSession?.LastSharedSequenceIndex ?? 0);
                await _conversationRepository.UpdateAsync(conversation, cancellationToken);
            }
        }
        else if (conversation.ProviderSessionId != activeSessionId)
        {
            conversation.SetProviderSessionId(activeSessionId);
            await _conversationRepository.UpdateAsync(conversation, cancellationToken);
        }
    }

    private string BuildEffectivePrompt(Conversation conversation, Guid userMsgId, string prompt)
    {
        var currentSession = conversation.ProviderSessions.FirstOrDefault(s => s.ProviderId.Equals(conversation.ProviderId, StringComparison.OrdinalIgnoreCase));
        var lastSharedIndex = currentSession?.LastSharedSequenceIndex ?? 0;
        var unsharedMessages = conversation.Messages
            .Where(m => m.Id != userMsgId && m.SequenceIndex > 0 && m.SequenceIndex > lastSharedIndex)
            .OrderBy(m => m.SequenceIndex)
            .ToList();

        var hasForeignMessages = unsharedMessages.Any(m => !string.IsNullOrEmpty(m.OriginProviderId) &&
                                                           !m.OriginProviderId.Equals(conversation.ProviderId, StringComparison.OrdinalIgnoreCase));

        var effectivePrompt = prompt;
        if (hasForeignMessages)
        {
            var contextBuilder = new System.Text.StringBuilder();
            _ = contextBuilder.AppendLine("[AI Agent Hub — Conversation Context Handoff]");
            _ = contextBuilder.AppendLine("The user switched to you from another AI provider in this ongoing conversation.");
            _ = contextBuilder.AppendLine("Below is the preceding conversation history for context:\n");
            _ = contextBuilder.AppendLine("--- Prior Conversation History ---");
            foreach (var msg in unsharedMessages)
            {
                var roleLabel = msg.Role == MessageRole.User ? "User" : $"Assistant ({msg.OriginProviderId ?? "AI"})";
                _ = contextBuilder.AppendLine($"{roleLabel}: {msg.Content}\n");
            }
            _ = contextBuilder.AppendLine("--- End of Prior History ---\n");
            _ = contextBuilder.AppendLine("Current user message:");
            _ = contextBuilder.AppendLine(prompt);
            effectivePrompt = contextBuilder.ToString();
        }

        _promptLogger.LogPromptExecution(
            conversation.Id,
            conversation.ProviderId,
            conversation.ModelId,
            effectivePrompt,
            hasForeignMessages,
            unsharedMessages.Count);

        return effectivePrompt;
    }

    private ProviderExecutionContext BuildExecutionContext(
        Conversation conversation,
        Workspace workspace,
        IProvider provider,
        ExecutionPromptContext promptContext,
        CancellationToken cancellationToken)
    {
        return new ProviderExecutionContext(
            conversation.Id,
            workspace.Id,
            workspace.Path,
            promptContext.EffectivePrompt,
            conversation.ModelId,
            conversation.ProviderSessionId,
            workspace.Settings.IgnoredFiles,
            async (token) =>
            {
                _ = promptContext.AssistantResponseBuilder.Append(token);
                await _broadcaster.SendMessageStreamChunkAsync(conversation.Id, token, promptContext.UserId, cancellationToken);
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
                var req = await _permissionService.RequestPermissionAsync(conversation.Id, provider.Id, permType, target, $"AI assistant requested {actionType} for {target}", promptContext.UserId, cancellationToken);
                return req.Decision == PermissionDecision.Approved;
            },
            cancellationToken,
            async (newSessionId) =>
            {
                if (!string.IsNullOrEmpty(newSessionId) && conversation.ProviderSessionId != newSessionId)
                {
                    conversation.SetProviderSessionId(newSessionId);
                    var existing = conversation.ProviderSessions.FirstOrDefault(s => s.ProviderId.Equals(conversation.ProviderId, StringComparison.OrdinalIgnoreCase));
                    var preservedIndex = existing?.LastSharedSequenceIndex ?? 0;
                    var preservedMsgId = existing?.LastSharedMessageId ?? promptContext.UserMsgId;
                    _ = conversation.AddOrUpdateProviderSession(conversation.ProviderId, newSessionId, preservedMsgId, preservedIndex);
                    await _conversationRepository.UpdateAsync(conversation, CancellationToken.None);
                }
            },
            conversation.Effort,
            OnHeartbeat: async (msg, elapsedSecs) =>
            {
                await _broadcaster.SendConversationEventAsync("conversation.heartbeat", conversation.Id, new
                {
                    message = msg,
                    elapsedSeconds = elapsedSecs
                }, promptContext.UserId, cancellationToken);
            }
        );
    }

    private async Task<(bool IsSuccess, string? Error, ProviderExecutionContext Context)> RunWithAutoResumeAsync(
        IProvider provider,
        ProviderExecutionContext execContext,
        System.Text.StringBuilder assistantResponseBuilder,
        string? userId,
        CancellationToken cancellationToken)
    {
        var maxAutoResumes = _options?.AutoResumeOnTimeout == true ? Math.Max(0, _options.MaxAutoResumes) : 0;
        var resumeCount = 0;

        while (true)
        {
            try
            {
                await provider.ExecuteAsync(execContext);
                return (true, null, execContext);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return (false, "AI response was cancelled by the user.", execContext);
            }
            catch (TimeoutException tex)
            {
                var (Continue, ResumeCount, Context, Error) = await TryResumeOrFailAsync(tex.Message, true, resumeCount, maxAutoResumes, execContext, userId, cancellationToken);
                if (Continue)
                {
                    resumeCount = ResumeCount;
                    execContext = Context;
                    continue;
                }

                return (false, Error, execContext);
            }
            catch (Exception ex)
            {
                var (Continue, ResumeCount, Context, Error) = await TryResumeOrFailAsync(ex.Message, IsTimeoutFailure(ex, assistantResponseBuilder.ToString()), resumeCount, maxAutoResumes, execContext, userId, cancellationToken);
                if (Continue)
                {
                    resumeCount = ResumeCount;
                    execContext = Context;
                    continue;
                }

                await _broadcaster.SendNotificationAsync("error", "AI Execution Failed", ex.Message, userId, cancellationToken);
                return (false, Error, execContext);
            }
        }
    }

    private async Task<(bool Continue, int ResumeCount, ProviderExecutionContext Context, string? Error)> TryResumeOrFailAsync(
        string failureMessage,
        bool retryable,
        int resumeCount,
        int maxAutoResumes,
        ProviderExecutionContext execContext,
        string? userId,
        CancellationToken cancellationToken)
    {
        if (retryable && resumeCount < maxAutoResumes && !cancellationToken.IsCancellationRequested)
        {
            await NotifyAutoResumeAsync(resumeCount + 1, maxAutoResumes, userId, cancellationToken);
            return (true, resumeCount + 1, execContext with { Prompt = "Continue from where you left off." }, null);
        }

        return (false, resumeCount, execContext, failureMessage);
    }

    private async Task NotifyAutoResumeAsync(int resumeCount, int maxAutoResumes, string? userId, CancellationToken cancellationToken) => await _broadcaster.SendNotificationAsync("info", "Auto-Resuming Execution", $"Prompt turn timed out. Automatically continuing from checkpoint (Attempt {resumeCount} of {maxAutoResumes})...", userId, cancellationToken);

    private static bool IsTimeoutFailure(Exception ex, string responseText)
    {
        return ex is TimeoutException
            || ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("deadline exceeded", StringComparison.OrdinalIgnoreCase)
            || responseText.Contains("timeout waiting for response", StringComparison.OrdinalIgnoreCase)
            || responseText.Contains("context deadline exceeded", StringComparison.OrdinalIgnoreCase)
            || responseText.Contains("timed out", StringComparison.OrdinalIgnoreCase)
            || responseText.Contains("print-timeout", StringComparison.OrdinalIgnoreCase);
    }

    private async Task RecordAssistantResponseAsync(
        Conversation conversation,
        IProvider provider,
        System.Text.StringBuilder assistantResponseBuilder,
        AssistantExecutionResult result,
        string? userId)
    {
        var metadata = new ExecutionMetadata
        {
            ProviderId = provider.Id,
            ModelId = conversation.ModelId,
            ProviderSessionId = conversation.ProviderSessionId,
            Timestamp = DateTimeOffset.UtcNow,
            DurationMs = result.ElapsedMilliseconds,
            IsSuccess = result.IsSuccess,
            ErrorMessage = result.Error
        };

        var finalAssistantText = assistantResponseBuilder.ToString().Trim();
        if (result.Error == "AI response was cancelled by the user." || (!result.IsSuccess && result.Error != null && result.Error.Contains("cancel", StringComparison.OrdinalIgnoreCase)))
        {
            finalAssistantText = string.IsNullOrWhiteSpace(finalAssistantText)
                ? "*(AI response was cancelled by the user.)*"
                : $"{finalAssistantText}\n\n*(AI response was cancelled by the user.)*";
        }
        else if (string.IsNullOrWhiteSpace(finalAssistantText) && !string.IsNullOrWhiteSpace(result.Error))
        {
            finalAssistantText = $"Error: {result.Error}";
        }

        var assistantMsg = conversation.AddMessage(MessageRole.Assistant, finalAssistantText, metadata, provider.Id, conversation.ModelId);
        _ = conversation.AddOrUpdateProviderSession(conversation.ProviderId, conversation.ProviderSessionId, assistantMsg.Id, assistantMsg.SequenceIndex);
        await _conversationRepository.UpdateAsync(conversation, CancellationToken.None);

        await _broadcaster.SendConversationEventAsync("conversation.completed", conversation.Id, new
        {
            DurationMs = result.ElapsedMilliseconds,
            result.FileChangesCount,
            result.IsSuccess
        }, userId, CancellationToken.None);
    }

    public async Task AbortAsync(Guid conversationId, string? userId = null)
    {
        if (_activeExecutionTracker.TryGetCancellationTokenSource(conversationId, out var cts))
        {
            try { if (cts != null) await cts.CancelAsync(); } catch { /* Non-fatal: CTS already cancelled or disposed */ }
        }

        var conversation = await _conversationRepository.GetByIdAsync(conversationId);
        if (conversation != null)
        {
            var provider = _providerManager.GetProvider(conversation.ProviderId);
            await provider.AbortAsync(conversationId);
            await _broadcaster.SendConversationEventAsync("conversation.aborted", conversationId, new
            {
                message = "AI response was cancelled by the user."
            }, userId, CancellationToken.None);
        }

        _ = _activeExecutionTracker.TryStop(conversationId);
    }

    private sealed record ExecutionPromptContext(
        string EffectivePrompt,
        System.Text.StringBuilder AssistantResponseBuilder,
        Guid UserMsgId,
        string? UserId);

    private sealed record AssistantExecutionResult(
        bool IsSuccess,
        string? Error,
        long ElapsedMilliseconds,
        int FileChangesCount);
}
