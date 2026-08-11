using AIAgentHub.Application.Security;
using AIAgentHub.Domain.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AIAgentHub.Infrastructure.Persistence;

public sealed class DatabaseResetter : IDatabaseResetter
{
    private readonly AgentHubDbContext _context;
    private readonly IServerSettingsRepository _settingsRepository;

    public DatabaseResetter(AgentHubDbContext context, IServerSettingsRepository settingsRepository)
    {
        _context = context;
        _settingsRepository = settingsRepository;
    }

    public async Task WipeAllDataAsync(CancellationToken cancellationToken = default)
    {
        _context.ChangeTracker.Clear();
        SqliteConnection.ClearAllPools();
        await _context.Database.EnsureDeletedAsync(cancellationToken);
        await _context.Database.MigrateAsync(cancellationToken);
        await _settingsRepository.GetAsync(cancellationToken);
    }
}
