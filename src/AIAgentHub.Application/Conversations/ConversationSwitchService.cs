using AIAgentHub.Application.Providers;
using AIAgentHub.Application.Realtime;
using AIAgentHub.Domain.Conversations;
using AIAgentHub.Domain.Repositories;

namespace AIAgentHub.Application.Conversations;

public sealed class ConversationSwitchService(
    IConversationRepository conversationRepository,
    IWorkspaceRepository workspaceRepository,
    IProviderManager providerManager,
    IAgentRealtimeBroadcaster broadcaster) : IConversationSwitchService
{
    private readonly IConversationRepository _conversationRepository = conversationRepository;
    private readonly IWorkspaceRepository _workspaceRepository = workspaceRepository;
    private readonly IProviderManager _providerManager = providerManager;
    private readonly IAgentRealtimeBroadcaster _broadcaster = broadcaster;

    public async Task<SwitchProviderResult> SwitchProviderAsync(
        Guid conversationId,
        SwitchProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var conversation = await _conversationRepository.GetByIdAsync(conversationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Conversation {conversationId} not found.");

        var workspace = await _workspaceRepository.GetByIdAsync(conversation.WorkspaceId, cancellationToken)
            ?? throw new KeyNotFoundException($"Workspace {conversation.WorkspaceId} not found.");

        var targetProvider = _providerManager.GetProvider(request.TargetProviderId);
        var targetInfo = await _providerManager.GetProviderInfoAsync(request.TargetProviderId, cancellationToken);
        if (targetInfo == null || targetInfo.Status != Domain.Providers.ProviderStatus.Ready || targetInfo.IsHidden)
        {
            throw new InvalidOperationException($"Provider '{targetInfo?.DisplayName ?? request.TargetProviderId}' is not operational (Status: {targetInfo?.Status}). Only active and ready-to-use providers can be selected.");
        }

        // 1. Lock conversation during switching handshake
        conversation.SetStatus(ConversationStatus.SwitchingProvider);
        await _conversationRepository.UpdateAsync(conversation, cancellationToken);
        await _broadcaster.SendConversationEventAsync(
            "conversation.status_changed",
            conversationId,
            new { Status = (int)ConversationStatus.SwitchingProvider, StatusName = nameof(ConversationStatus.SwitchingProvider) },
            cancellationToken);

        try
        {
            // 2. Identify target session & determine interactions to migrate
            var existingSession = conversation.ProviderSessions.FirstOrDefault(s =>
                s.ProviderId.Equals(request.TargetProviderId, StringComparison.OrdinalIgnoreCase));

            var allMessages = conversation.Messages
                .OrderBy(m => m.SequenceIndex > 0 ? m.SequenceIndex : int.MaxValue)
                .ThenBy(m => m.CreatedAtUtc)
                .ToList();

            var userPrompts = allMessages.Where(m => m.Role == MessageRole.User).ToList();
            int initialSharedIndex;
            int migratedInteractionsCount;

            var scope = request.HistoryScope?.Trim().ToLowerInvariant() ?? "all";
            if (scope == "delta" || (scope == "auto" && existingSession != null))
            {
                initialSharedIndex = existingSession?.LastSharedSequenceIndex ?? 0;
                migratedInteractionsCount = initialSharedIndex == 0
                    ? userPrompts.Count
                    : userPrompts.Count(m => m.SequenceIndex > initialSharedIndex);
            }
            else if (scope.StartsWith("recent_") && int.TryParse(scope.AsSpan(7), out var recentCount) && recentCount > 0)
            {
                if (userPrompts.Count > recentCount)
                {
                    var targetPrompt = userPrompts[userPrompts.Count - recentCount];
                    initialSharedIndex = Math.Max(0, targetPrompt.SequenceIndex - 1);
                    migratedInteractionsCount = recentCount;
                }
                else
                {
                    initialSharedIndex = 0;
                    migratedInteractionsCount = userPrompts.Count;
                }
            }
            else if (scope == "none")
            {
                // None: 0 interactions migrated.
                // Keep target provider's existing checkpoint without advancing it,
                // so if the user performs no interaction in the new provider, they can return and still migrate history.
                initialSharedIndex = existingSession?.LastSharedSequenceIndex ?? 0;
                migratedInteractionsCount = 0;
            }
            else
            {
                initialSharedIndex = 0;
                migratedInteractionsCount = userPrompts.Count;
            }

            // 3. Start or attach target session
            var targetSessionId = existingSession?.ProviderSessionId;
            if (string.IsNullOrEmpty(targetSessionId))
            {
                targetSessionId = await targetProvider.StartSessionAsync(
                    conversation.Id,
                    workspace.Path,
                    request.TargetModelId,
                    cancellationToken);
            }

            // 4. Update session checkpoint (sets starting unshared boundary for subsequent prompt turns)
            if (scope != "none")
            {
                _ = conversation.AddOrUpdateProviderSession(
                    request.TargetProviderId,
                    targetSessionId,
                    existingSession?.LastSharedMessageId,
                    initialSharedIndex);
            }
            else if (existingSession != null && !string.IsNullOrEmpty(targetSessionId) && existingSession.ProviderSessionId != targetSessionId)
            {
                existingSession.UpdateCheckpoint(existingSession.LastSharedMessageId, existingSession.LastSharedSequenceIndex, targetSessionId);
            }

            // 5. Update active provider, reset model to requested model or null ("Default"), unlock
            conversation.SetProviderAndModel(request.TargetProviderId, request.TargetModelId);
            conversation.SetProviderSessionId(targetSessionId);
            conversation.SetStatus(ConversationStatus.Active);

            await _conversationRepository.UpdateAsync(conversation, cancellationToken);

            await _broadcaster.SendConversationEventAsync(
                "conversation.switched_provider",
                conversationId,
                new
                {
                    ActiveProviderId = conversation.ProviderId,
                    ActiveModelId = conversation.ModelId,
                    MigratedMessageCount = migratedInteractionsCount,
                    TargetSessionId = targetSessionId
                },
                cancellationToken);

            return new SwitchProviderResult(
                conversation.Id,
                conversation.ProviderId,
                conversation.ModelId,
                migratedInteractionsCount,
                targetSessionId);
        }
        finally
        {
            if (conversation.Status == ConversationStatus.SwitchingProvider)
            {
                conversation.SetStatus(ConversationStatus.Active);
                await _conversationRepository.UpdateAsync(conversation, cancellationToken);
                await _broadcaster.SendConversationEventAsync(
                    "conversation.status_changed",
                    conversationId,
                    new { Status = (int)ConversationStatus.Active, StatusName = nameof(ConversationStatus.Active) },
                    cancellationToken);
            }
        }
    }

    public async Task<IReadOnlyList<ConversationProviderSessionDto>> GetSessionsAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        var conversation = await _conversationRepository.GetByIdAsync(conversationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Conversation {conversationId} not found.");

        return conversation.ProviderSessions
            .OrderByDescending(s => s.LastActiveAtUtc)
            .Select(s => new ConversationProviderSessionDto(
                s.Id,
                s.ConversationId,
                s.ProviderId,
                s.ProviderSessionId,
                s.LastSharedMessageId,
                s.LastSharedSequenceIndex,
                s.CreatedAtUtc,
                s.LastActiveAtUtc))
            .ToList();
    }

    public async Task<ConversationDetailDto> AbortSwitchAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        var conversation = await _conversationRepository.GetByIdAsync(conversationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Conversation {conversationId} not found.");

        if (conversation.Status == ConversationStatus.SwitchingProvider)
        {
            conversation.SetStatus(ConversationStatus.Active);
            await _conversationRepository.UpdateAsync(conversation, cancellationToken);
            await _broadcaster.SendConversationEventAsync(
                "conversation.status_changed",
                conversationId,
                new { Status = (int)ConversationStatus.Active, StatusName = nameof(ConversationStatus.Active) },
                cancellationToken);
        }

        var messages = conversation.Messages
            .OrderBy(m => m.SequenceIndex > 0 ? m.SequenceIndex : 0)
            .ThenBy(m => m.CreatedAtUtc)
            .Select(m => new MessageDto(
                m.Id,
                m.ConversationId,
                m.Role,
                m.Content,
                m.CreatedAtUtc,
                m.Metadata,
                m.SequenceIndex,
                m.OriginProviderId,
                m.OriginModelId))
            .ToList();

        var sessions = conversation.ProviderSessions
            .OrderByDescending(s => s.LastActiveAtUtc)
            .Select(s => new ConversationProviderSessionDto(
                s.Id,
                s.ConversationId,
                s.ProviderId,
                s.ProviderSessionId,
                s.LastSharedMessageId,
                s.LastSharedSequenceIndex,
                s.CreatedAtUtc,
                s.LastActiveAtUtc))
            .ToList();

        return new ConversationDetailDto(
            conversation.Id,
            conversation.WorkspaceId,
            conversation.Title,
            conversation.ProviderId,
            conversation.ModelId,
            conversation.Effort,
            conversation.CreatedAtUtc,
            conversation.UpdatedAtUtc,
            messages,
            conversation.LastUserInteractionAtUtc,
            conversation.Status,
            conversation.IsPinned,
            sessions
        );
    }
}
