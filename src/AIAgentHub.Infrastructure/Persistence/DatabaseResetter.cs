using AIAgentHub.Application.Security;
using AIAgentHub.Domain.Repositories;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AIAgentHub.Infrastructure.Persistence;

public sealed class DatabaseResetter(AgentHubDbContext context, IServerSettingsRepository settingsRepository) : IDatabaseResetter
{
    private readonly AgentHubDbContext _context = context;
    private readonly IServerSettingsRepository _settingsRepository = settingsRepository;

    public async Task WipeAllDataAsync(CancellationToken cancellationToken = default)
    {
        _context.ChangeTracker.Clear();
        SqliteConnection.ClearAllPools();
        _ = await _context.Database.EnsureDeletedAsync(cancellationToken);

        // Recreate the schema through the migration pipeline, NOT EnsureCreated:
        // EnsureCreated bypasses __EFMigrationsHistory, so the next startup's
        // MigrateAsync would replay all migrations against the existing tables
        // and crash (e.g. 'table "FileSnapshots" already exists').
        await _context.Database.MigrateAsync(cancellationToken);
        _ = await _settingsRepository.GetAsync(cancellationToken);
    }
}
