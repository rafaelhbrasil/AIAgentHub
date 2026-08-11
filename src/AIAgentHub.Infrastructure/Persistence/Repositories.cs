using AIAgentHub.Domain.Conversations;
using AIAgentHub.Domain.FileChanges;
using AIAgentHub.Domain.Mcp;
using AIAgentHub.Domain.Permissions;
using AIAgentHub.Domain.Repositories;
using AIAgentHub.Domain.Security;
using AIAgentHub.Domain.Skills;
using AIAgentHub.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace AIAgentHub.Infrastructure.Persistence;

public sealed class WorkspaceRepository : IWorkspaceRepository
{
    private readonly AgentHubDbContext _context;

    public WorkspaceRepository(AgentHubDbContext context) => _context = context;

    public async Task<IReadOnlyList<Workspace>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var list = await _context.Workspaces
            .Include(w => w.Conversations)
            .ToListAsync(cancellationToken);

        return list.OrderByDescending(w => w.LastAccessedAtUtc).ToList();
    }

    public async Task<Workspace?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Workspaces
            .Include(w => w.Conversations)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
    }

    public async Task<Workspace?> GetByPathAsync(string path, CancellationToken cancellationToken = default)
    {
        var normalized = Path.GetFullPath(path);
        return await _context.Workspaces
            .Include(w => w.Conversations)
            .FirstOrDefaultAsync(w => w.Path == normalized, cancellationToken);
    }

    public async Task AddAsync(Workspace workspace, CancellationToken cancellationToken = default)
    {
        await _context.Workspaces.AddAsync(workspace, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Workspace workspace, CancellationToken cancellationToken = default)
    {
        _context.Workspaces.Update(workspace);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var ws = await _context.Workspaces.FindAsync(new object[] { id }, cancellationToken);
        if (ws != null)
        {
            _context.Workspaces.Remove(ws);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

public sealed class ConversationRepository : IConversationRepository
{
    private readonly AgentHubDbContext _context;

    public ConversationRepository(AgentHubDbContext context) => _context = context;

    public async Task<IReadOnlyList<Conversation>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        var list = await _context.Conversations
            .Include(c => c.Messages)
            .Include(c => c.FileChanges)
            .Where(c => c.WorkspaceId == workspaceId)
            .ToListAsync(cancellationToken);

        return list.OrderByDescending(c => c.UpdatedAtUtc).ToList();
    }

    public async Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Conversations
            .Include(c => c.Messages)
            .Include(c => c.FileChanges)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        await _context.Conversations.AddAsync(conversation, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        foreach (var message in conversation.Messages)
        {
            var entry = _context.Entry(message);
            if (entry.State == EntityState.Detached || entry.State == EntityState.Modified)
            {
                var exists = await _context.Messages.AnyAsync(m => m.Id == message.Id, cancellationToken);
                if (!exists)
                {
                    entry.State = EntityState.Added;
                }
            }
        }

        foreach (var fileChange in conversation.FileChanges)
        {
            var entry = _context.Entry(fileChange);
            if (entry.State == EntityState.Detached || entry.State == EntityState.Modified)
            {
                var exists = await _context.FileChanges.AnyAsync(fc => fc.Id == fileChange.Id, cancellationToken);
                if (!exists)
                {
                    entry.State = EntityState.Added;
                }
            }
        }

        if (_context.Entry(conversation).State == EntityState.Detached)
        {
            _context.Conversations.Update(conversation);
        }
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var conv = await _context.Conversations.FindAsync(new object[] { id }, cancellationToken);
        if (conv != null)
        {
            _context.Conversations.Remove(conv);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

public sealed class UserAccountRepository : IUserAccountRepository
{
    private readonly AgentHubDbContext _context;

    public UserAccountRepository(AgentHubDbContext context) => _context = context;

    public async Task<UserAccount?> GetAdminAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Users.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(UserAccount account, CancellationToken cancellationToken = default)
    {
        await _context.Users.AddAsync(account, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(UserAccount account, CancellationToken cancellationToken = default)
    {
        _context.Users.Update(account);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAllAsync(CancellationToken cancellationToken = default)
    {
        _context.Users.RemoveRange(_context.Users);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

public sealed class ServerSettingsRepository : IServerSettingsRepository
{
    private readonly AgentHubDbContext _context;

    public ServerSettingsRepository(AgentHubDbContext context) => _context = context;

    public async Task<ServerSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _context.ServerSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings == null)
        {
            settings = new ServerSettings
            {
                IsSetupCompleted = false,
                NetworkMode = NetworkMode.Localhost,
                ListeningPortHttps = 5432,
                ListeningPortHttp = 5433,
                Theme = "dark"
            };
            await _context.ServerSettings.AddAsync(settings, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
        return settings;
    }

    public async Task UpdateAsync(ServerSettings settings, CancellationToken cancellationToken = default)
    {
        _context.ServerSettings.Update(settings);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

public sealed class FileChangeRepository : IFileChangeRepository
{
    private readonly AgentHubDbContext _context;

    public FileChangeRepository(AgentHubDbContext context) => _context = context;

    public async Task<IReadOnlyList<FileChange>> GetByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        var list = await _context.FileChanges
            .Where(fc => fc.ConversationId == conversationId)
            .ToListAsync(cancellationToken);

        return list.OrderByDescending(fc => fc.CreatedAtUtc).ToList();
    }

    public async Task<FileChange?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.FileChanges.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task AddAsync(FileChange change, CancellationToken cancellationToken = default)
    {
        await _context.FileChanges.AddAsync(change, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(FileChange change, CancellationToken cancellationToken = default)
    {
        _context.FileChanges.Update(change);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

public sealed class FileSnapshotRepository : IFileSnapshotRepository
{
    private readonly AgentHubDbContext _context;

    public FileSnapshotRepository(AgentHubDbContext context) => _context = context;

    public async Task<IReadOnlyList<FileSnapshot>> GetByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        return await _context.FileSnapshots
            .Where(fs => fs.ConversationId == conversationId)
            .ToListAsync(cancellationToken);
    }

    public async Task<FileSnapshot?> GetLatestByPathAsync(Guid workspaceId, string relativePath, CancellationToken cancellationToken = default)
    {
        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        var list = await _context.FileSnapshots
            .Where(fs => fs.WorkspaceId == workspaceId && fs.RelativePath == normalized)
            .ToListAsync(cancellationToken);

        return list.OrderByDescending(fs => fs.CapturedAtUtc).FirstOrDefault();
    }

    public async Task AddAsync(FileSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        await _context.FileSnapshots.AddAsync(snapshot, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

public sealed class EncryptedSecretRepository : IEncryptedSecretRepository
{
    private readonly AgentHubDbContext _context;

    public EncryptedSecretRepository(AgentHubDbContext context) => _context = context;

    public async Task<IReadOnlyList<EncryptedSecret>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Secrets.ToListAsync(cancellationToken);
    }

    public async Task<EncryptedSecret?> GetAsync(string providerId, string keyName, CancellationToken cancellationToken = default)
    {
        return await _context.Secrets
            .FirstOrDefaultAsync(s => s.ProviderId == providerId && s.KeyName == keyName, cancellationToken);
    }

    public async Task UpsertAsync(EncryptedSecret secret, CancellationToken cancellationToken = default)
    {
        var existing = await GetAsync(secret.ProviderId, secret.KeyName, cancellationToken);
        if (existing != null)
        {
            existing.CiphertextBase64 = secret.CiphertextBase64;
            existing.NonceBase64 = secret.NonceBase64;
            existing.TagBase64 = secret.TagBase64;
            existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
            _context.Secrets.Update(existing);
        }
        else
        {
            await _context.Secrets.AddAsync(secret, cancellationToken);
        }
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string providerId, string keyName, CancellationToken cancellationToken = default)
    {
        var existing = await GetAsync(providerId, keyName, cancellationToken);
        if (existing != null)
        {
            _context.Secrets.Remove(existing);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

public sealed class SkillRepository : ISkillRepository
{
    private readonly AgentHubDbContext _context;

    public SkillRepository(AgentHubDbContext context) => _context = context;

    public async Task<IReadOnlyList<Skill>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Skills.ToListAsync(cancellationToken);
    }

    public async Task<Skill?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Skills.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task UpsertAsync(Skill skill, CancellationToken cancellationToken = default)
    {
        var existing = await GetByIdAsync(skill.Id, cancellationToken);
        if (existing != null)
        {
            _context.Entry(existing).CurrentValues.SetValues(skill);
        }
        else
        {
            await _context.Skills.AddAsync(skill, cancellationToken);
        }
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await GetByIdAsync(id, cancellationToken);
        if (existing != null)
        {
            _context.Skills.Remove(existing);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

public sealed class McpServerRepository : IMcpServerRepository
{
    private readonly AgentHubDbContext _context;

    public McpServerRepository(AgentHubDbContext context) => _context = context;

    public async Task<IReadOnlyList<McpServer>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.McpServers.ToListAsync(cancellationToken);
    }

    public async Task<McpServer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.McpServers.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task UpsertAsync(McpServer server, CancellationToken cancellationToken = default)
    {
        var existing = await GetByIdAsync(server.Id, cancellationToken);
        if (existing != null)
        {
            _context.Entry(existing).CurrentValues.SetValues(server);
        }
        else
        {
            await _context.McpServers.AddAsync(server, cancellationToken);
        }
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await GetByIdAsync(id, cancellationToken);
        if (existing != null)
        {
            _context.McpServers.Remove(existing);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

public sealed class PermissionRequestRepository : IPermissionRequestRepository
{
    private readonly AgentHubDbContext _context;

    public PermissionRequestRepository(AgentHubDbContext context) => _context = context;

    public async Task<IReadOnlyList<PermissionRequest>> GetByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        var list = await _context.PermissionRequests
            .Where(p => p.ConversationId == conversationId)
            .ToListAsync(cancellationToken);

        return list.OrderByDescending(p => p.RequestedAtUtc).ToList();
    }

    public async Task<PermissionRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.PermissionRequests.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task AddAsync(PermissionRequest request, CancellationToken cancellationToken = default)
    {
        await _context.PermissionRequests.AddAsync(request, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(PermissionRequest request, CancellationToken cancellationToken = default)
    {
        _context.PermissionRequests.Update(request);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

public sealed class ProviderModelSettingRepository : IProviderModelSettingRepository
{
    private readonly AgentHubDbContext _context;

    public ProviderModelSettingRepository(AgentHubDbContext context) => _context = context;

    private async Task EnsureTableCreatedAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""ProviderModelSettings"" (
                    ""Id"" TEXT NOT NULL CONSTRAINT ""PK_ProviderModelSettings"" PRIMARY KEY,
                    ""ProviderId"" TEXT NOT NULL,
                    ""ModelId"" TEXT NOT NULL,
                    ""IsDisplayed"" INTEGER NOT NULL,
                    ""UpdatedAtUtc"" TEXT NOT NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ProviderModelSettings_ProviderId_ModelId"" ON ""ProviderModelSettings"" (""ProviderId"", ""ModelId"");
            ", cancellationToken);
        }
        catch
        {
            // Ignore if concurrently created or unsupported
        }
    }

    public async Task<IReadOnlyList<AIAgentHub.Domain.Providers.ProviderModelSetting>> GetByProviderIdAsync(string providerId, CancellationToken cancellationToken = default)
    {
        await EnsureTableCreatedAsync(cancellationToken);
        return await _context.ProviderModelSettings
            .Where(s => s.ProviderId == providerId)
            .ToListAsync(cancellationToken);
    }

    public async Task ReconcileAsync(string providerId, IReadOnlyList<AIAgentHub.Domain.Providers.ModelInfo> currentModels, CancellationToken cancellationToken = default)
    {
        await EnsureTableCreatedAsync(cancellationToken);
        List<AIAgentHub.Domain.Providers.ProviderModelSetting> existingSettings;
        try
        {
            existingSettings = await _context.ProviderModelSettings
                .Where(s => s.ProviderId == providerId)
                .ToListAsync(cancellationToken);
        }
        catch
        {
            await EnsureTableCreatedAsync(cancellationToken);
            existingSettings = await _context.ProviderModelSettings
                .Where(s => s.ProviderId == providerId)
                .ToListAsync(cancellationToken);
        }

        var existingMap = existingSettings.ToDictionary(s => s.ModelId, s => s, StringComparer.OrdinalIgnoreCase);
        var currentModelIds = new HashSet<string>(currentModels.Select(m => m.Id), StringComparer.OrdinalIgnoreCase);

        var obsolete = existingSettings.Where(s => !currentModelIds.Contains(s.ModelId)).ToList();
        if (obsolete.Count > 0)
        {
            _context.ProviderModelSettings.RemoveRange(obsolete);
        }

        var hasChanges = obsolete.Count > 0;
        foreach (var m in currentModels)
        {
            if (existingMap.TryGetValue(m.Id, out var setting))
            {
                m.IsDisplayed = setting.IsDisplayed;
            }
            else
            {
                m.IsDisplayed = true;
                _context.ProviderModelSettings.Add(new AIAgentHub.Domain.Providers.ProviderModelSetting
                {
                    ProviderId = providerId,
                    ModelId = m.Id,
                    IsDisplayed = true,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                });
                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task UpdateSettingsAsync(string providerId, Dictionary<string, bool> modelStates, CancellationToken cancellationToken = default)
    {
        await EnsureTableCreatedAsync(cancellationToken);
        var existingSettings = await _context.ProviderModelSettings
            .Where(s => s.ProviderId == providerId)
            .ToListAsync(cancellationToken);

        var existingMap = existingSettings.ToDictionary(s => s.ModelId, s => s, StringComparer.OrdinalIgnoreCase);

        foreach (var (modelId, isDisplayed) in modelStates)
        {
            if (existingMap.TryGetValue(modelId, out var setting))
            {
                setting.IsDisplayed = isDisplayed;
                setting.UpdatedAtUtc = DateTimeOffset.UtcNow;
            }
            else
            {
                _context.ProviderModelSettings.Add(new AIAgentHub.Domain.Providers.ProviderModelSetting
                {
                    ProviderId = providerId,
                    ModelId = modelId,
                    IsDisplayed = isDisplayed,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
