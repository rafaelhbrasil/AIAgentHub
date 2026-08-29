namespace AIAgentHub.Application.Conversations;

public sealed record SwitchProviderRequest(
    string TargetProviderId,
    string? TargetModelId = null,
    string HistoryScope = "all",
    bool IncludeFileChanges = true);

public sealed record SwitchProviderResult(
    Guid ConversationId,
    string ActiveProviderId,
    string? ActiveModelId,
    int MigratedMessageCount,
    string? TargetSessionId);

public sealed record ConversationProviderSessionDto(
    Guid Id,
    Guid ConversationId,
    string ProviderId,
    string? ProviderSessionId,
    Guid? LastSharedMessageId,
    int LastSharedSequenceIndex,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastActiveAtUtc);

public interface IConversationSwitchService
{
    public Task<SwitchProviderResult> SwitchProviderAsync(
        Guid conversationId,
        SwitchProviderRequest request,
        CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<ConversationProviderSessionDto>> GetSessionsAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default);

    public Task<ConversationDetailDto> AbortSwitchAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default);
}
