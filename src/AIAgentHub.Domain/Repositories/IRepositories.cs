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
    public Task<IReadOnlyList<Workspace>> GetAllAsync(CancellationToken cancellationToken = default);
    public Task<Workspace?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    public Task<Workspace?> GetByPathAsync(string path, CancellationToken cancellationToken = default);
    public Task AddAsync(Workspace workspace, CancellationToken cancellationToken = default);
    public Task UpdateAsync(Workspace workspace, CancellationToken cancellationToken = default);
    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IConversationRepository
{
    public Task<IReadOnlyList<Conversation>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default);
    public Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    public Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default);
    public Task UpdateAsync(Conversation conversation, CancellationToken cancellationToken = default);
    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IUserAccountRepository
{
    public Task<UserAccount?> GetAdminAsync(CancellationToken cancellationToken = default);
    public Task AddAsync(UserAccount account, CancellationToken cancellationToken = default);
    public Task UpdateAsync(UserAccount account, CancellationToken cancellationToken = default);
    public Task DeleteAllAsync(CancellationToken cancellationToken = default);
}

public interface IServerSettingsRepository
{
    public Task<ServerSettings> GetAsync(CancellationToken cancellationToken = default);
    public Task UpdateAsync(ServerSettings settings, CancellationToken cancellationToken = default);
}

public interface IFileChangeRepository
{
    public Task<IReadOnlyList<FileChange>> GetByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default);
    public Task<FileChange?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    public Task AddAsync(FileChange change, CancellationToken cancellationToken = default);
    public Task UpdateAsync(FileChange change, CancellationToken cancellationToken = default);
    public Task DeleteAsync(FileChange change, CancellationToken cancellationToken = default);
}

public interface IFileSnapshotRepository
{
    public Task<IReadOnlyList<FileSnapshot>> GetByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default);
    public Task<FileSnapshot?> GetLatestByPathAsync(Guid workspaceId, string relativePath, CancellationToken cancellationToken = default);
    public Task AddAsync(FileSnapshot snapshot, CancellationToken cancellationToken = default);
}

public interface IEncryptedSecretRepository
{
    public Task<IReadOnlyList<EncryptedSecret>> GetAllAsync(CancellationToken cancellationToken = default);
    public Task<EncryptedSecret?> GetAsync(string providerId, string keyName, CancellationToken cancellationToken = default);
    public Task UpsertAsync(EncryptedSecret secret, CancellationToken cancellationToken = default);
    public Task DeleteAsync(string providerId, string keyName, CancellationToken cancellationToken = default);
}

public interface ISkillRepository
{
    public Task<IReadOnlyList<Skill>> GetAllAsync(CancellationToken cancellationToken = default);
    public Task<Skill?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    public Task UpsertAsync(Skill skill, CancellationToken cancellationToken = default);
    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IMcpServerRepository
{
    public Task<IReadOnlyList<McpServer>> GetAllAsync(CancellationToken cancellationToken = default);
    public Task<McpServer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    public Task UpsertAsync(McpServer server, CancellationToken cancellationToken = default);
    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IPermissionRequestRepository
{
    public Task<IReadOnlyList<PermissionRequest>> GetByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default);
    public Task<PermissionRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    public Task AddAsync(PermissionRequest request, CancellationToken cancellationToken = default);
    public Task UpdateAsync(PermissionRequest request, CancellationToken cancellationToken = default);
}

public interface IProviderModelSettingRepository
{
    public Task<IReadOnlyList<Providers.ProviderModelSetting>> GetByProviderIdAsync(string providerId, CancellationToken cancellationToken = default);
    public Task ReconcileAsync(string providerId, IReadOnlyList<Providers.ModelInfo> currentModels, CancellationToken cancellationToken = default);
    public Task UpdateSettingsAsync(string providerId, Dictionary<string, bool> modelStates, CancellationToken cancellationToken = default);
}

public interface IProviderDetectionRecordRepository
{
    public Task<IReadOnlyList<Providers.ProviderDetectionRecord>> GetAllAsync(CancellationToken cancellationToken = default);
    public Task<Providers.ProviderDetectionRecord?> GetByProviderIdAsync(string providerId, CancellationToken cancellationToken = default);
    public Task UpsertAsync(Providers.ProviderDetectionRecord record, CancellationToken cancellationToken = default);
    public Task DeleteAsync(string providerId, CancellationToken cancellationToken = default);
}
