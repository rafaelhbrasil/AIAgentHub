using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Conversations;
using AIAgentHub.Domain.Repositories;

namespace AIAgentHub.Application.Conversations;

public sealed class ConversationService(
    IConversationRepository conversationRepository,
    IWorkspaceRepository workspaceRepository,
    IProviderManager? providerManager = null) : IConversationService
{
    private readonly IConversationRepository _conversationRepository = conversationRepository;
    private readonly IWorkspaceRepository _workspaceRepository = workspaceRepository;
    private readonly IProviderManager? _providerManager = providerManager;

    public async Task<IReadOnlyList<ConversationDto>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        var list = await _conversationRepository.GetByWorkspaceIdAsync(workspaceId, cancellationToken);
        return list
            .Select(MapToDto)
            .OrderByDescending(c => c.IsPinned)
            .ThenByDescending(c => c.LastUserInteractionAtUtc ?? c.UpdatedAtUtc)
            .ToList();
    }

    public async Task<ConversationDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var conv = await _conversationRepository.GetByIdAsync(id, cancellationToken);
        if (conv == null)
        {
            return null;
        }

        var messages = conv.Messages
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

        var sessions = conv.ProviderSessions
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
            conv.Id,
            conv.WorkspaceId,
            conv.Title,
            conv.ProviderId,
            conv.ModelId,
            conv.Effort,
            conv.CreatedAtUtc,
            conv.UpdatedAtUtc,
            messages,
            conv.LastUserInteractionAtUtc,
            conv.Status,
            conv.IsPinned,
            sessions
        );
    }

    public async Task<ConversationDto> CreateAsync(CreateConversationRequest request, CancellationToken cancellationToken = default)
    {
        var workspace = await _workspaceRepository.GetByIdAsync(request.WorkspaceId, cancellationToken) ?? throw new KeyNotFoundException($"Workspace with ID {request.WorkspaceId} not found.");

        string? providerId = null;
        if (!string.IsNullOrWhiteSpace(request.ProviderId))
        {
            providerId = request.ProviderId.Trim();
        }
        else if (!string.IsNullOrWhiteSpace(workspace.Settings.DefaultProviderId))
        {
            providerId = workspace.Settings.DefaultProviderId.Trim();
        }
        else if (_providerManager != null)
        {
            var providers = await _providerManager.GetAllAsync(cancellationToken);
            var firstReady = providers.FirstOrDefault(p => p.Status == Domain.Providers.ProviderStatus.Ready && !p.IsHidden);
            providerId = firstReady?.Id;
        }

        if (string.IsNullOrWhiteSpace(providerId))
        {
            throw new InvalidOperationException("No operational AI providers are currently available. Please configure at least one provider in the AI Providers settings.");
        }

        var modelId = request.ModelId ?? workspace.Settings.DefaultModelId;

        var conversation = Conversation.Create(request.WorkspaceId, request.Title, providerId, modelId, request.ProviderSessionId, request.Effort, request.IsPinned);
        await _conversationRepository.AddAsync(conversation, cancellationToken);

        workspace.Touch();
        await _workspaceRepository.UpdateAsync(workspace, cancellationToken);

        return MapToDto(conversation);
    }

    public async Task<ConversationDto> RenameAsync(Guid id, string newTitle, CancellationToken cancellationToken = default)
    {
        var conversation = await _conversationRepository.GetByIdAsync(id, cancellationToken) ?? throw new KeyNotFoundException($"Conversation with ID {id} not found.");
        conversation.Rename(newTitle);
        await _conversationRepository.UpdateAsync(conversation, cancellationToken);

        return MapToDto(conversation);
    }

    public async Task<ConversationDto> SetPinnedAsync(Guid id, bool isPinned, CancellationToken cancellationToken = default)
    {
        var conversation = await _conversationRepository.GetByIdAsync(id, cancellationToken) ?? throw new KeyNotFoundException($"Conversation with ID {id} not found.");
        conversation.SetPinned(isPinned);
        await _conversationRepository.UpdateAsync(conversation, cancellationToken);

        return MapToDto(conversation);
    }

    public async Task SetProviderAndModelAsync(Guid id, string providerId, string? modelId, string? effort = null, CancellationToken cancellationToken = default)
    {
        var conversation = await _conversationRepository.GetByIdAsync(id, cancellationToken) ?? throw new KeyNotFoundException($"Conversation with ID {id} not found.");
        conversation.SetProviderAndModel(providerId, modelId, effort);
        await _conversationRepository.UpdateAsync(conversation, cancellationToken);
    }

    public async Task<MessageDto> AddMessageAsync(
        Guid conversationId,
        MessageRole role,
        string content,
        ExecutionMetadata? metadata = null,
        string? originProviderId = null,
        string? originModelId = null,
        CancellationToken cancellationToken = default)
    {
        var conversation = await _conversationRepository.GetByIdAsync(conversationId, cancellationToken) ?? throw new KeyNotFoundException($"Conversation with ID {conversationId} not found.");
        var msg = conversation.AddMessage(role, content, metadata, originProviderId, originModelId);
        await _conversationRepository.UpdateAsync(conversation, cancellationToken);

        return new MessageDto(
            msg.Id,
            msg.ConversationId,
            msg.Role,
            msg.Content,
            msg.CreatedAtUtc,
            msg.Metadata,
            msg.SequenceIndex,
            msg.OriginProviderId,
            msg.OriginModelId);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => await _conversationRepository.DeleteAsync(id, cancellationToken);

    public async Task<IReadOnlyList<ConversationDto>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<ConversationDto>();
        }

        var workspaces = await _workspaceRepository.GetAllAsync(cancellationToken);
        var allConversations = new List<ConversationDto>();

        foreach (var ws in workspaces)
        {
            var convs = await _conversationRepository.GetByWorkspaceIdAsync(ws.Id, cancellationToken);
            foreach (var c in convs)
            {
                if (c.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    c.Messages.Any(m => m.Content.Contains(query, StringComparison.OrdinalIgnoreCase)))
                {
                    allConversations.Add(MapToDto(c));
                }
            }
        }

        return allConversations
            .OrderByDescending(c => c.IsPinned)
            .ThenByDescending(c => c.LastUserInteractionAtUtc ?? c.UpdatedAtUtc)
            .ToList();
    }

    private static ConversationDto MapToDto(Conversation c)
    {
        return new ConversationDto(
            c.Id,
            c.WorkspaceId,
            c.Title,
            c.ProviderId,
            c.ModelId,
            c.Effort,
            c.CreatedAtUtc,
            c.UpdatedAtUtc,
            c.Messages.Count,
            c.FileChanges.Count,
            c.LastUserInteractionAtUtc,
            c.Status,
            c.IsPinned
        );
    }
}
