using AIAgentHub.Application.Filesystem;
using AIAgentHub.Application.Git;
using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Repositories;
using AIAgentHub.Domain.Workspaces;

namespace AIAgentHub.Application.Workspaces;

public sealed class WorkspaceService(
    IWorkspaceRepository workspaceRepository,
    IFilesystemService filesystemService,
    ISystemPathValidator systemPathValidator,
    IProviderManager providerManager,
    IGitService? gitService = null) : IWorkspaceService
{
    private readonly IWorkspaceRepository _workspaceRepository = workspaceRepository;
    private readonly IFilesystemService _filesystemService = filesystemService;
    private readonly ISystemPathValidator _systemPathValidator = systemPathValidator;
    private readonly IProviderManager _providerManager = providerManager;
    private readonly IGitService? _gitService = gitService;

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
        var rawPath = request.Path?.Trim() ?? string.Empty;
        if (_systemPathValidator.IsPathForbidden(rawPath, out var reason))
        {
            throw new ArgumentException(reason ?? $"Directory '{rawPath}' is not allowed as a workspace.");
        }

        var fullPath = Path.GetFullPath(rawPath);
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

        string? defaultProviderId = null;
        if (!string.IsNullOrWhiteSpace(request.DefaultProviderId))
        {
            defaultProviderId = request.DefaultProviderId.Trim();
        }
        else if (_providerManager != null)
        {
            var providers = await _providerManager.GetAllAsync(cancellationToken);
            var firstReady = providers.FirstOrDefault(p => p.Status == Domain.Providers.ProviderStatus.Ready && !p.IsHidden);
            defaultProviderId = firstReady?.Id;
        }

        var settings = new WorkspaceSettings
        {
            DefaultProviderId = defaultProviderId,
            DefaultModelId = request.DefaultModelId
        };

        var workspace = Workspace.Create(name, fullPath, request.Origin, settings, request.IsFavorite);
        await _workspaceRepository.AddAsync(workspace, cancellationToken);

        return MapToDto(workspace);
    }

    public async Task<WorkspaceDto> CloneAsync(CloneWorkspaceRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CloneUrl))
        {
            throw new ArgumentException("Clone URL cannot be empty.", nameof(request));
        }

        var rawTarget = request.TargetDirectory?.Trim() ?? string.Empty;
        if (_systemPathValidator.IsPathForbidden(rawTarget, out var reason))
        {
            throw new ArgumentException(reason ?? $"Directory '{rawTarget}' is not allowed as a workspace.");
        }

        var fullPath = Path.GetFullPath(rawTarget);
        if (_gitService != null)
        {
            var cloneResult = await _gitService.CloneAsync(request.CloneUrl, fullPath, cancellationToken);
            if (!cloneResult.Success)
            {
                throw new InvalidOperationException(cloneResult.Error ?? "Git clone failed.");
            }
        }
        else
        {
            throw new InvalidOperationException("Git service is not available.");
        }

        var existing = await _workspaceRepository.GetByPathAsync(fullPath, cancellationToken);
        if (existing != null)
        {
            existing.Touch();
            await _workspaceRepository.UpdateAsync(existing, cancellationToken);
            return MapToDto(existing);
        }

        var name = string.IsNullOrWhiteSpace(request.WorkspaceName)
            ? _filesystemService.SuggestWorkspaceName(fullPath)
            : request.WorkspaceName.Trim();

        string? defaultProviderId = null;
        if (!string.IsNullOrWhiteSpace(request.DefaultProviderId))
        {
            defaultProviderId = request.DefaultProviderId.Trim();
        }
        else if (_providerManager != null)
        {
            var providers = await _providerManager.GetAllAsync(cancellationToken);
            var firstReady = providers.FirstOrDefault(p => p.Status == Domain.Providers.ProviderStatus.Ready && !p.IsHidden);
            defaultProviderId = firstReady?.Id;
        }

        var settings = new WorkspaceSettings
        {
            DefaultProviderId = defaultProviderId,
            DefaultModelId = request.DefaultModelId
        };

        var workspace = Workspace.Create(name, fullPath, WorkspaceOrigin.Git, settings);
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

    public async Task<WorkspaceDto> SetFavoriteAsync(Guid id, bool isFavorite, CancellationToken cancellationToken = default)
    {
        var workspace = await _workspaceRepository.GetByIdAsync(id, cancellationToken) ?? throw new KeyNotFoundException($"Workspace with ID {id} not found.");
        workspace.SetFavorite(isFavorite);
        await _workspaceRepository.UpdateAsync(workspace, cancellationToken);
        return MapToDto(workspace);
    }

    public async Task<WorkspaceDto> SetArchivedAsync(Guid id, bool isArchived, CancellationToken cancellationToken = default)
    {
        var workspace = await _workspaceRepository.GetByIdAsync(id, cancellationToken) ?? throw new KeyNotFoundException($"Workspace with ID {id} not found.");
        workspace.SetArchived(isArchived);
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
            w.Conversations.Count,
            w.IsFavorite,
            w.IsArchived
        );
    }
}
