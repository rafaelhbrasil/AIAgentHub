using AIAgentHub.Domain.Conversations;

namespace AIAgentHub.Application.Conversations;

public sealed record MessageDto(
    Guid Id,
    Guid ConversationId,
    MessageRole Role,
    string Content,
    DateTimeOffset CreatedAtUtc,
    ExecutionMetadata? Metadata,
    int SequenceIndex = 0,
    string? OriginProviderId = null,
    string? OriginModelId = null);

public sealed record ConversationDto(
    Guid Id,
    Guid WorkspaceId,
    string Title,
    string ProviderId,
    string? ModelId,
    string? Effort,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    int MessageCount,
    int FileChangeCount,
    DateTimeOffset? LastUserInteractionAtUtc = null,
    ConversationStatus Status = ConversationStatus.Active,
    bool IsPinned = false);

public sealed record ConversationDetailDto(
    Guid Id,
    Guid WorkspaceId,
    string Title,
    string ProviderId,
    string? ModelId,
    string? Effort,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<MessageDto> Messages,
    DateTimeOffset? LastUserInteractionAtUtc = null,
    ConversationStatus Status = ConversationStatus.Active,
    bool IsPinned = false,
    IReadOnlyList<ConversationProviderSessionDto>? Sessions = null);

public sealed record CreateConversationRequest(
    Guid WorkspaceId,
    string Title,
    string? ProviderId = null,
    string? ModelId = null,
    string? ProviderSessionId = null,
    string? Effort = null,
    bool IsPinned = false);

public interface IConversationService
{
    public Task<IReadOnlyList<ConversationDto>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default);
    public Task<ConversationDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    public Task<ConversationDto> CreateAsync(CreateConversationRequest request, CancellationToken cancellationToken = default);
    public Task<ConversationDto> RenameAsync(Guid id, string newTitle, CancellationToken cancellationToken = default);
    public Task<ConversationDto> SetPinnedAsync(Guid id, bool isPinned, CancellationToken cancellationToken = default);
    public Task SetProviderAndModelAsync(Guid id, string providerId, string? modelId, string? effort = null, CancellationToken cancellationToken = default);
    public Task<MessageDto> AddMessageAsync(Guid conversationId, MessageRole role, string content, ExecutionMetadata? metadata = null, string? originProviderId = null, string? originModelId = null, CancellationToken cancellationToken = default);
    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    public Task<IReadOnlyList<ConversationDto>> SearchAsync(string query, CancellationToken cancellationToken = default);
}
