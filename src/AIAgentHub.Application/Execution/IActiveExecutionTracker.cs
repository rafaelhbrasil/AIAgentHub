using System.Collections.Concurrent;

namespace AIAgentHub.Application.Execution;

public interface IActiveExecutionTracker
{
    public bool TryStart(Guid conversationId, CancellationTokenSource cts);
    public bool TryStop(Guid conversationId);
    public bool IsExecuting(Guid conversationId);
    public bool TryGetCancellationTokenSource(Guid conversationId, out CancellationTokenSource? cts);

    public bool TryStartExecution(Guid conversationId, CancellationTokenSource cts) => TryStart(conversationId, cts);
    public bool CompleteExecution(Guid conversationId) => TryStop(conversationId);
}

public sealed class ActiveExecutionTracker : IActiveExecutionTracker
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _active = new();

    public bool TryStart(Guid conversationId, CancellationTokenSource cts) => _active.TryAdd(conversationId, cts);

    public bool TryStop(Guid conversationId) => _active.TryRemove(conversationId, out _);

    public bool IsExecuting(Guid conversationId) => _active.ContainsKey(conversationId);

    public bool TryGetCancellationTokenSource(Guid conversationId, out CancellationTokenSource? cts) => _active.TryGetValue(conversationId, out cts);

    public bool TryStartExecution(Guid conversationId, CancellationTokenSource cts) => TryStart(conversationId, cts);
    public bool CompleteExecution(Guid conversationId) => TryStop(conversationId);
}
