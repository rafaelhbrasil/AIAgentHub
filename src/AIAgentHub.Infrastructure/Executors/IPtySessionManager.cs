namespace AIAgentHub.Infrastructure.Executors;

public record PtySessionInfo(
    Guid ConversationId,
    string Executable,
    string WorkspacePath,
    bool IsActive,
    DateTimeOffset StartedAtUtc,
    int? ThinkingTokens = null,
    string? ActiveBackgroundTask = null,
    string? CurrentTool = null);

public interface IPtySessionManager
{
    Task<PtySessionInfo> GetOrCreateSessionAsync(Guid conversationId, string workspacePath, int cols = 120, int rows = 30, CancellationToken cancellationToken = default);
    Task SendInputAsync(Guid conversationId, string input, CancellationToken cancellationToken = default);
    Task ResizeSessionAsync(Guid conversationId, int cols, int rows, CancellationToken cancellationToken = default);
    Task TerminateSessionAsync(Guid conversationId, CancellationToken cancellationToken = default);
    string GetRecentOutput(Guid conversationId);
    PtySessionInfo? GetSessionInfo(Guid conversationId);
    bool HasActiveSession(Guid conversationId);
}
