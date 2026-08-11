using AIAgentHub.Domain.Conversations;
using AIAgentHub.Domain.FileChanges;
using AIAgentHub.Domain.Mcp;
using AIAgentHub.Domain.Permissions;
using AIAgentHub.Domain.Security;
using AIAgentHub.Domain.Skills;
using AIAgentHub.Domain.Workspaces;

namespace AIAgentHub.Domain.Repositories;

public interface IWorkspaceRepository
{
    Task<IReadOnlyList<Workspace>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Workspace?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Workspace?> GetByPathAsync(string path, CancellationToken cancellationToken = default);
    Task AddAsync(Workspace workspace, CancellationToken cancellationToken = default);
    Task UpdateAsync(Workspace workspace, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IConversationRepository
{
    Task<IReadOnlyList<Conversation>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default);
    Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default);
    Task UpdateAsync(Conversation conversation, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IUserAccountRepository
{
    Task<UserAccount?> GetAdminAsync(CancellationToken cancellationToken = default);
    Task AddAsync(UserAccount account, CancellationToken cancellationToken = default);
    Task UpdateAsync(UserAccount account, CancellationToken cancellationToken = default);
    Task DeleteAllAsync(CancellationToken cancellationToken = default);
}

public interface IServerSettingsRepository
{
    Task<ServerSettings> GetAsync(CancellationToken cancellationToken = default);
    Task UpdateAsync(ServerSettings settings, CancellationToken cancellationToken = default);
}

public interface IFileChangeRepository
{
    Task<IReadOnlyList<FileChange>> GetByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default);
    Task<FileChange?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(FileChange change, CancellationToken cancellationToken = default);
    Task UpdateAsync(FileChange change, CancellationToken cancellationToken = default);
}

public interface IFileSnapshotRepository
{
    Task<IReadOnlyList<FileSnapshot>> GetByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default);
    Task<FileSnapshot?> GetLatestByPathAsync(Guid workspaceId, string relativePath, CancellationToken cancellationToken = default);
    Task AddAsync(FileSnapshot snapshot, CancellationToken cancellationToken = default);
}

public interface IEncryptedSecretRepository
{
    Task<IReadOnlyList<EncryptedSecret>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<EncryptedSecret?> GetAsync(string providerId, string keyName, CancellationToken cancellationToken = default);
    Task UpsertAsync(EncryptedSecret secret, CancellationToken cancellationToken = default);
    Task DeleteAsync(string providerId, string keyName, CancellationToken cancellationToken = default);
}

public interface ISkillRepository
{
    Task<IReadOnlyList<Skill>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Skill?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpsertAsync(Skill skill, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IMcpServerRepository
{
    Task<IReadOnlyList<McpServer>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<McpServer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpsertAsync(McpServer server, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IPermissionRequestRepository
{
    Task<IReadOnlyList<PermissionRequest>> GetByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default);
    Task<PermissionRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(PermissionRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(PermissionRequest request, CancellationToken cancellationToken = default);
}

public interface IProviderModelSettingRepository
{
    Task<IReadOnlyList<AIAgentHub.Domain.Providers.ProviderModelSetting>> GetByProviderIdAsync(string providerId, CancellationToken cancellationToken = default);
    Task ReconcileAsync(string providerId, IReadOnlyList<AIAgentHub.Domain.Providers.ModelInfo> currentModels, CancellationToken cancellationToken = default);
    Task UpdateSettingsAsync(string providerId, Dictionary<string, bool> modelStates, CancellationToken cancellationToken = default);
}

