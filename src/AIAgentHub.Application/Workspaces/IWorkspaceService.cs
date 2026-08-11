using AIAgentHub.Domain.Workspaces;

namespace AIAgentHub.Application.Workspaces;

public sealed record WorkspaceDto(
    Guid Id,
    string Name,
    string Path,
    WorkspaceOrigin Origin,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastAccessedAtUtc,
    WorkspaceSettings Settings,
    int ConversationCount);

public sealed record CreateWorkspaceRequest(
    string Name,
    string Path,
    WorkspaceOrigin Origin = WorkspaceOrigin.Server,
    string? DefaultProviderId = "gemini",
    string? DefaultModelId = null);

public sealed record UpdateWorkspaceRequest(
    string Name,
    WorkspaceSettings Settings);

public interface IWorkspaceService
{
    Task<IReadOnlyList<WorkspaceDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<WorkspaceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WorkspaceDto> CreateAsync(CreateWorkspaceRequest request, CancellationToken cancellationToken = default);
    Task<WorkspaceDto> UpdateAsync(Guid id, UpdateWorkspaceRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task TouchAsync(Guid id, CancellationToken cancellationToken = default);
}
