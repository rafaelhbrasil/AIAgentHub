using AIAgentHub.Domain.Conversations;
using AIAgentHub.Domain.FileChanges;
using AIAgentHub.Domain.Mcp;
using AIAgentHub.Domain.Permissions;
using AIAgentHub.Domain.Providers;
using AIAgentHub.Domain.Repositories;
using AIAgentHub.Domain.Security;
using AIAgentHub.Domain.Skills;
using AIAgentHub.Domain.Workspaces;

using Microsoft.EntityFrameworkCore;

namespace AIAgentHub.Infrastructure.Persistence;

public sealed class WorkspaceRepository(AgentHubDbContext context) : IWorkspaceRepository
{
    private readonly AgentHubDbContext _context = context;

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
        _ = await _context.Workspaces.AddAsync(workspace, cancellationToken);
        _ = await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Workspace workspace, CancellationToken cancellationToken = default)
    {
        _ = _context.Workspaces.Update(workspace);
        _ = await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var ws = await _context.Workspaces.FindAsync([id], cancellationToken);
        if (ws != null)
        {
            _ = _context.Workspaces.Remove(ws);
            _ = await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

public sealed class ConversationRepository(AgentHubDbContext context) : IConversationRepository
{
    private readonly AgentHubDbContext _context = context;

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
        _ = await _context.Conversations.AddAsync(conversation, cancellationToken);
        _ = await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        foreach (var message in conversation.Messages)
        {
            var entry = _context.Entry(message);
            if (entry.State is EntityState.Detached or EntityState.Modified)
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
            if (entry.State is EntityState.Detached or EntityState.Modified)
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
            _ = _context.Conversations.Update(conversation);
        }
        _ = await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var conv = await _context.Conversations.FindAsync([id], cancellationToken);
        if (conv != null)
        {
            _ = _context.Conversations.Remove(conv);
            _ = await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

public sealed class UserAccountRepository(AgentHubDbContext context) : IUserAccountRepository
{
    private readonly AgentHubDbContext _context = context;

    public async Task<UserAccount?> GetAdminAsync(CancellationToken cancellationToken = default) => await _context.Users.FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(UserAccount account, CancellationToken cancellationToken = default)
    {
        _ = await _context.Users.AddAsync(account, cancellationToken);
        _ = await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(UserAccount account, CancellationToken cancellationToken = default)
    {
        _ = _context.Users.Update(account);
        _ = await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAllAsync(CancellationToken cancellationToken = default)
    {
        _context.Users.RemoveRange(_context.Users);
        _ = await _context.SaveChangesAsync(cancellationToken);
    }
}

public sealed class ServerSettingsRepository(AgentHubDbContext context) : IServerSettingsRepository
{
    private readonly AgentHubDbContext _context = context;

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
            _ = await _context.ServerSettings.AddAsync(settings, cancellationToken);
            _ = await _context.SaveChangesAsync(cancellationToken);
        }
        return settings;
    }

    public async Task UpdateAsync(ServerSettings settings, CancellationToken cancellationToken = default)
    {
        _ = _context.ServerSettings.Update(settings);
        _ = await _context.SaveChangesAsync(cancellationToken);
    }
}

public sealed class FileChangeRepository(AgentHubDbContext context) : IFileChangeRepository
{
    private readonly AgentHubDbContext _context = context;

    public async Task<IReadOnlyList<FileChange>> GetByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        var list = await _context.FileChanges
            .Where(fc => fc.ConversationId == conversationId)
            .ToListAsync(cancellationToken);

        return list.OrderByDescending(fc => fc.CreatedAtUtc).ToList();
    }

    public async Task<FileChange?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => await _context.FileChanges.FindAsync([id], cancellationToken);

    public async Task AddAsync(FileChange change, CancellationToken cancellationToken = default)
    {
        _ = await _context.FileChanges.AddAsync(change, cancellationToken);
        _ = await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(FileChange change, CancellationToken cancellationToken = default)
    {
        _ = _context.FileChanges.Update(change);
        _ = await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(FileChange change, CancellationToken cancellationToken = default)
    {
        _ = _context.FileChanges.Remove(change);
        _ = await _context.SaveChangesAsync(cancellationToken);
    }
}

public sealed class FileSnapshotRepository(AgentHubDbContext context) : IFileSnapshotRepository
{
    private readonly AgentHubDbContext _context = context;

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
        _ = await _context.FileSnapshots.AddAsync(snapshot, cancellationToken);
        _ = await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(FileSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        _ = _context.FileSnapshots.Remove(snapshot);
        _ = await _context.SaveChangesAsync(cancellationToken);
    }
}

public sealed class EncryptedSecretRepository(AgentHubDbContext context) : IEncryptedSecretRepository
{
    private readonly AgentHubDbContext _context = context;

    public async Task<IReadOnlyList<EncryptedSecret>> GetAllAsync(CancellationToken cancellationToken = default) => await _context.Secrets.ToListAsync(cancellationToken);

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
            _ = _context.Secrets.Update(existing);
        }
        else
        {
            _ = await _context.Secrets.AddAsync(secret, cancellationToken);
        }
        _ = await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string providerId, string keyName, CancellationToken cancellationToken = default)
    {
        var existing = await GetAsync(providerId, keyName, cancellationToken);
        if (existing != null)
        {
            _ = _context.Secrets.Remove(existing);
            _ = await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

public sealed class SkillRepository(AgentHubDbContext context) : ISkillRepository
{
    private readonly AgentHubDbContext _context = context;

    public async Task<IReadOnlyList<Skill>> GetAllAsync(CancellationToken cancellationToken = default) => await _context.Skills.ToListAsync(cancellationToken);

    public async Task<Skill?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => await _context.Skills.FindAsync([id], cancellationToken);

    public async Task UpsertAsync(Skill skill, CancellationToken cancellationToken = default)
    {
        var existing = await GetByIdAsync(skill.Id, cancellationToken);
        if (existing != null)
        {
            _context.Entry(existing).CurrentValues.SetValues(skill);
        }
        else
        {
            _ = await _context.Skills.AddAsync(skill, cancellationToken);
        }
        _ = await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await GetByIdAsync(id, cancellationToken);
        if (existing != null)
        {
            _ = _context.Skills.Remove(existing);
            _ = await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

public sealed class McpServerRepository(AgentHubDbContext context) : IMcpServerRepository
{
    private readonly AgentHubDbContext _context = context;

    public async Task<IReadOnlyList<McpServer>> GetAllAsync(CancellationToken cancellationToken = default) => await _context.McpServers.ToListAsync(cancellationToken);

    public async Task<McpServer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => await _context.McpServers.FindAsync([id], cancellationToken);

    public async Task UpsertAsync(McpServer server, CancellationToken cancellationToken = default)
    {
        var existing = await GetByIdAsync(server.Id, cancellationToken);
        if (existing != null)
        {
            _context.Entry(existing).CurrentValues.SetValues(server);
        }
        else
        {
            _ = await _context.McpServers.AddAsync(server, cancellationToken);
        }
        _ = await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await GetByIdAsync(id, cancellationToken);
        if (existing != null)
        {
            _ = _context.McpServers.Remove(existing);
            _ = await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

public sealed class PermissionRequestRepository(AgentHubDbContext context) : IPermissionRequestRepository
{
    private readonly AgentHubDbContext _context = context;

    public async Task<IReadOnlyList<PermissionRequest>> GetByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        var list = await _context.PermissionRequests
            .Where(p => p.ConversationId == conversationId)
            .ToListAsync(cancellationToken);

        return list.OrderByDescending(p => p.RequestedAtUtc).ToList();
    }

    public async Task<PermissionRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => await _context.PermissionRequests.FindAsync([id], cancellationToken);

    public async Task AddAsync(PermissionRequest request, CancellationToken cancellationToken = default)
    {
        _ = await _context.PermissionRequests.AddAsync(request, cancellationToken);
        _ = await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(PermissionRequest request, CancellationToken cancellationToken = default)
    {
        _ = _context.PermissionRequests.Update(request);
        _ = await _context.SaveChangesAsync(cancellationToken);
    }
}

public sealed class ProviderDetectionRecordRepository(AgentHubDbContext context) : IProviderDetectionRecordRepository
{
    private readonly AgentHubDbContext _context = context;

    public async Task<IReadOnlyList<ProviderDetectionRecord>> GetAllAsync(CancellationToken cancellationToken = default) => await _context.ProviderDetectionRecords.ToListAsync(cancellationToken);

    public async Task<ProviderDetectionRecord?> GetByProviderIdAsync(string providerId, CancellationToken cancellationToken = default)
    {
        return await _context.ProviderDetectionRecords
            .FirstOrDefaultAsync(r => r.ProviderId == providerId, cancellationToken);
    }

    public async Task UpsertAsync(ProviderDetectionRecord record, CancellationToken cancellationToken = default)
    {
        var existing = await _context.ProviderDetectionRecords
            .FirstOrDefaultAsync(r => r.ProviderId == record.ProviderId, cancellationToken);

        if (existing != null)
        {
            existing.Status = record.Status;
            existing.StatusDetails = record.StatusDetails;
            existing.Version = record.Version;
            existing.ExecutablePath = record.ExecutablePath;
            existing.IsInstalled = record.IsInstalled;
            existing.IsAuthenticated = record.IsAuthenticated;
            existing.QuotaResetsAt = record.QuotaResetsAt;
            existing.DetectedAtUtc = record.DetectedAtUtc;
        }
        else
        {
            _ = _context.ProviderDetectionRecords.Add(record);
        }

        _ = await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string providerId, CancellationToken cancellationToken = default)
    {
        var existing = await _context.ProviderDetectionRecords
            .FirstOrDefaultAsync(r => r.ProviderId == providerId, cancellationToken);
        if (existing != null)
        {
            _ = _context.ProviderDetectionRecords.Remove(existing);
            _ = await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

public sealed class ProviderModelSettingRepository(AgentHubDbContext context) : IProviderModelSettingRepository
{
    private readonly AgentHubDbContext _context = context;

    public async Task<IReadOnlyList<ProviderModelSetting>> GetByProviderIdAsync(string providerId, CancellationToken cancellationToken = default)
    {
        return await _context.ProviderModelSettings
            .Where(s => s.ProviderId == providerId)
            .ToListAsync(cancellationToken);
    }

    public async Task ReconcileAsync(string providerId, IReadOnlyList<ModelInfo> currentModels, CancellationToken cancellationToken = default)
    {
        var existingSettings = await _context.ProviderModelSettings
            .Where(s => s.ProviderId == providerId)
            .ToListAsync(cancellationToken);

        var existingMap = existingSettings.ToDictionary(s => s.ModelId, s => s, StringComparer.OrdinalIgnoreCase);
        var validModels = currentModels
            .Where(m => !string.IsNullOrWhiteSpace(m.Id) && !m.Id.Trim().Equals("default", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var currentModelIds = new HashSet<string>(validModels.Select(m => m.Id), StringComparer.OrdinalIgnoreCase);

        var obsolete = existingSettings.Where(s => !currentModelIds.Contains(s.ModelId)).ToList();
        if (obsolete.Count > 0)
        {
            _context.ProviderModelSettings.RemoveRange(obsolete);
        }

        var hasChanges = obsolete.Count > 0;
        foreach (var m in validModels)
        {
            if (existingMap.TryGetValue(m.Id, out var setting))
            {
                m.IsDisplayed = setting.IsDisplayed;
                if (setting.DisplayName != m.DisplayName ||
                    setting.Description != m.Description ||
                    setting.ContextWindow != m.ContextWindow ||
                    setting.IsDefault != m.IsDefault)
                {
                    setting.DisplayName = m.DisplayName;
                    setting.Description = m.Description;
                    setting.ContextWindow = m.ContextWindow;
                    setting.IsDefault = m.IsDefault;
                    setting.UpdatedAtUtc = DateTimeOffset.UtcNow;
                    hasChanges = true;
                }
            }
            else
            {
                m.IsDisplayed = true;
                _ = _context.ProviderModelSettings.Add(new ProviderModelSetting
                {
                    ProviderId = providerId,
                    ModelId = m.Id,
                    DisplayName = m.DisplayName,
                    Description = m.Description,
                    ContextWindow = m.ContextWindow,
                    IsDefault = m.IsDefault,
                    IsDisplayed = true,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                });
                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            _ = await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task UpdateSettingsAsync(string providerId, Dictionary<string, bool> modelStates, CancellationToken cancellationToken = default)
    {
        var existingSettings = await _context.ProviderModelSettings
            .Where(s => s.ProviderId == providerId)
            .ToListAsync(cancellationToken);

        var existingMap = existingSettings.ToDictionary(s => s.ModelId, s => s, StringComparer.OrdinalIgnoreCase);

        foreach (var (modelId, isDisplayed) in modelStates)
        {
            if (string.IsNullOrWhiteSpace(modelId) || modelId.Trim().Equals("default", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (existingMap.TryGetValue(modelId, out var setting))
            {
                setting.IsDisplayed = isDisplayed;
                setting.UpdatedAtUtc = DateTimeOffset.UtcNow;
            }
            else
            {
                _ = _context.ProviderModelSettings.Add(new ProviderModelSetting
                {
                    ProviderId = providerId,
                    ModelId = modelId,
                    IsDisplayed = isDisplayed,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                });
            }
        }

        _ = await _context.SaveChangesAsync(cancellationToken);
    }
}
