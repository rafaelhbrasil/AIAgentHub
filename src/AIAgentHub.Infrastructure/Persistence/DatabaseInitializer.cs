using AIAgentHub.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AIAgentHub.Infrastructure.Persistence;

public sealed class DatabaseInitializer
{
    private readonly AgentHubDbContext _context;
    private readonly IServerSettingsRepository _settingsRepository;

    public DatabaseInitializer(AgentHubDbContext context, IServerSettingsRepository settingsRepository)
    {
        _context = context;
        _settingsRepository = settingsRepository;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.Database.MigrateAsync(cancellationToken);
        }
        catch
        {
            await _context.Database.EnsureCreatedAsync(cancellationToken);
        }

        try
        {
            // Ensure default settings record exists
            await _settingsRepository.GetAsync(cancellationToken);
        }
        catch
        {
            // Ignore if concurrently initialized
        }
    }
}
