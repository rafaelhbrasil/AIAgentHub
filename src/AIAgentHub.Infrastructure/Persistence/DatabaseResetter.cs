using AIAgentHub.Application.Security;
using AIAgentHub.Domain.Repositories;

using Microsoft.Data.Sqlite;

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
        _ = await _context.Database.EnsureCreatedAsync(cancellationToken);
        _ = await _settingsRepository.GetAsync(cancellationToken);
    }
}
