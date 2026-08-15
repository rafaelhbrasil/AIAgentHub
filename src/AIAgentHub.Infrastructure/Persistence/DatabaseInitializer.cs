using AIAgentHub.Domain.Repositories;

namespace AIAgentHub.Infrastructure.Persistence;

public sealed class DatabaseInitializer(AgentHubDbContext context, IServerSettingsRepository settingsRepository)
{
    private readonly AgentHubDbContext _context = context;
    private readonly IServerSettingsRepository _settingsRepository = settingsRepository;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _ = await _context.Database.EnsureCreatedAsync(cancellationToken);

        try
        {
            // Ensure default settings record exists
            _ = await _settingsRepository.GetAsync(cancellationToken);
        }
        catch
        {
            // Ignore if concurrently initialized
        }
    }
}
