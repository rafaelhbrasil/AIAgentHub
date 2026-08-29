using AIAgentHub.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AIAgentHub.Infrastructure.Persistence;

public sealed class DatabaseInitializer(AgentHubDbContext context, IServerSettingsRepository settingsRepository)
{
    private readonly AgentHubDbContext _context = context;
    private readonly IServerSettingsRepository _settingsRepository = settingsRepository;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _ = await _context.Database.ExecuteSqlRawAsync(
                "DELETE FROM \"__EFMigrationsLock\";", cancellationToken);
        }
        catch
        {
            // Table might not exist yet on initial database creation, safe to ignore
        }

        await _context.Database.MigrateAsync(cancellationToken);

        try
        {
            // Ensure default settings record exists
            _ = await _settingsRepository.GetAsync(cancellationToken);
        }
        catch
        {
            // Ignore if concurrently initialized
        }

        await HealLegacyDataAsync(cancellationToken);
    }

    private async Task HealLegacyDataAsync(CancellationToken cancellationToken)
    {
        var conversations = await _context.Conversations
            .Include(c => c.Messages)
            .Include(c => c.ProviderSessions)
            .ToListAsync(cancellationToken);

        var hasChanges = false;
        foreach (var conv in conversations)
        {
            // 1. Heal message sequence indexes and attribution if missing
            var messages = conv.Messages.OrderBy(m => m.CreatedAtUtc).ToList();
            for (var i = 0; i < messages.Count; i++)
            {
                var msg = messages[i];
                var expectedIndex = i + 1;
                if (msg.SequenceIndex == 0)
                {
                    msg.SetSequenceIndex(expectedIndex);
                    hasChanges = true;
                }

                if (string.IsNullOrEmpty(msg.OriginProviderId) && !string.IsNullOrEmpty(conv.ProviderId))
                {
                    msg.SetOrigin(conv.ProviderId, conv.ModelId);
                    hasChanges = true;
                }
            }

            // 2. Backfill initial ConversationProviderSession for original provider if missing
            if (!string.IsNullOrEmpty(conv.ProviderId) &&
                !conv.ProviderSessions.Any(s => s.ProviderId.Equals(conv.ProviderId, StringComparison.OrdinalIgnoreCase)))
            {
                var lastMsg = messages.LastOrDefault();
                var session = conv.AddOrUpdateProviderSession(
                    conv.ProviderId,
                    conv.ProviderSessionId,
                    lastMsg?.Id,
                    lastMsg?.SequenceIndex ?? messages.Count);
                _context.Entry(session).State = EntityState.Added;
                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            _ = await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
