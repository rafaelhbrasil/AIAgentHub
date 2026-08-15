using AIAgentHub.Application.Filesystem;
using AIAgentHub.Domain.Repositories;
using AIAgentHub.Domain.Workspaces;

namespace AIAgentHub.Application.Workspaces;

public sealed class WorkspaceService(IWorkspaceRepository workspaceRepository, IFilesystemService filesystemService) : IWorkspaceService
{
    private readonly IWorkspaceRepository _workspaceRepository = workspaceRepository;
    private readonly IFilesystemService _filesystemService = filesystemService;

    public async Task<IReadOnlyList<WorkspaceDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var list = await _workspaceRepository.GetAllAsync(cancellationToken);
        return list.Select(MapToDto).ToList();
    }

    public async Task<WorkspaceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var workspace = await _workspaceRepository.GetByIdAsync(id, cancellationToken);
        return workspace == null ? null : MapToDto(workspace);
    }

    public async Task<WorkspaceDto> CreateAsync(CreateWorkspaceRequest request, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(request.Path.Trim());
        if (!Directory.Exists(fullPath))
        {
            _ = Directory.CreateDirectory(fullPath);
        }

        var existing = await _workspaceRepository.GetByPathAsync(fullPath, cancellationToken);
        if (existing != null)
        {
            existing.Touch();
            await _workspaceRepository.UpdateAsync(existing, cancellationToken);
            return MapToDto(existing);
        }

        var name = string.IsNullOrWhiteSpace(request.Name)
            ? _filesystemService.SuggestWorkspaceName(fullPath)
            : request.Name.Trim();

        var settings = new WorkspaceSettings
        {
            DefaultProviderId = request.DefaultProviderId ?? "gemini",
            DefaultModelId = request.DefaultModelId
        };

        var workspace = Workspace.Create(name, fullPath, request.Origin, settings);
        await _workspaceRepository.AddAsync(workspace, cancellationToken);

        return MapToDto(workspace);
    }

    public async Task<WorkspaceDto> UpdateAsync(Guid id, UpdateWorkspaceRequest request, CancellationToken cancellationToken = default)
    {
        var workspace = await _workspaceRepository.GetByIdAsync(id, cancellationToken) ?? throw new KeyNotFoundException($"Workspace with ID {id} not found.");
        workspace.Rename(request.Name);
        workspace.UpdateSettings(request.Settings);

        await _workspaceRepository.UpdateAsync(workspace, cancellationToken);
        return MapToDto(workspace);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => await _workspaceRepository.DeleteAsync(id, cancellationToken);

    public async Task TouchAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var workspace = await _workspaceRepository.GetByIdAsync(id, cancellationToken);
        if (workspace != null)
        {
            workspace.Touch();
            await _workspaceRepository.UpdateAsync(workspace, cancellationToken);
        }
    }

    private static WorkspaceDto MapToDto(Workspace w)
    {
        return new WorkspaceDto(
            w.Id,
            w.Name,
            w.Path,
            w.Origin,
            w.CreatedAtUtc,
            w.LastAccessedAtUtc,
            w.Settings,
            w.Conversations.Count
        );
    }
}
