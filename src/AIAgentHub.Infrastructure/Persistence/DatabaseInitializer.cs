using AIAgentHub.Domain.Conversations;
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
        // 1. Fast guard: exit immediately if all messages and sessions are already healed (0 rows loaded into memory)
        var needsMessageHealing = await _context.Messages
            .AnyAsync(m => m.SequenceIndex == 0 || string.IsNullOrEmpty(m.OriginProviderId), cancellationToken);

        var needsSessionHealing = await _context.Conversations
            .AnyAsync(c => !string.IsNullOrEmpty(c.ProviderId) && !c.ProviderSessions.Any(), cancellationToken);

        if (!needsMessageHealing && !needsSessionHealing)
        {
            return;
        }

        // 2. LINQ projection with query syntax: select only the conversation ID, required scalar attributes,
        // and message projection (Id, SequenceIndex, OriginProviderId), avoiding heavy text content or metadata.
        var legacyConvs = await (
            from c in _context.Conversations
            where c.Messages.Any(m => m.SequenceIndex == 0 || string.IsNullOrEmpty(m.OriginProviderId))
               || (!string.IsNullOrEmpty(c.ProviderId) && !c.ProviderSessions.Any())
            select new
            {
                c.Id,
                c.ProviderId,
                c.ModelId,
                c.ProviderSessionId,
                HasProviderSession = c.ProviderSessions.Any(s => s.ProviderId == c.ProviderId),
                Messages = (
                    from m in c.Messages
                    select new
                    {
                        m.Id,
                        m.CreatedAtUtc,
                        m.SequenceIndex,
                        m.OriginProviderId
                    }
                ).ToList()
            }
        ).ToListAsync(cancellationToken);

        foreach (var conv in legacyConvs)
        {
            var lastMsgId = (Guid?)null;
            var lastMsgSequenceIndex = 0;
            var messages = conv.Messages.OrderBy(m => m.CreatedAtUtc).ToList();

            for (var i = 0; i < messages.Count; i++)
            {
                var msg = messages[i];
                var expectedIndex = i + 1;
                var needsIndexUpdate = msg.SequenceIndex == 0;
                var needsOriginUpdate = string.IsNullOrEmpty(msg.OriginProviderId) && !string.IsNullOrEmpty(conv.ProviderId);

                if (needsIndexUpdate && needsOriginUpdate)
                {
                    _ = await _context.Messages
                        .Where(m => m.Id == msg.Id)
                        .ExecuteUpdateAsync(setter => setter
                            .SetProperty(m => m.SequenceIndex, expectedIndex)
                            .SetProperty(m => m.OriginProviderId, conv.ProviderId)
                            .SetProperty(m => m.OriginModelId, conv.ModelId), cancellationToken);
                }
                else if (needsIndexUpdate)
                {
                    _ = await _context.Messages
                        .Where(m => m.Id == msg.Id)
                        .ExecuteUpdateAsync(setter => setter
                            .SetProperty(m => m.SequenceIndex, expectedIndex), cancellationToken);
                }
                else if (needsOriginUpdate)
                {
                    _ = await _context.Messages
                        .Where(m => m.Id == msg.Id)
                        .ExecuteUpdateAsync(setter => setter
                            .SetProperty(m => m.OriginProviderId, conv.ProviderId)
                            .SetProperty(m => m.OriginModelId, conv.ModelId), cancellationToken);
                }

                lastMsgId = msg.Id;
                lastMsgSequenceIndex = expectedIndex;
            }

            // Backfill initial ConversationProviderSession for original provider if missing
            if (!string.IsNullOrEmpty(conv.ProviderId) && !conv.HasProviderSession)
            {
                var session = ConversationProviderSession.Create(
                    conv.Id,
                    conv.ProviderId,
                    conv.ProviderSessionId,
                    lastMsgId,
                    lastMsgSequenceIndex > 0 ? lastMsgSequenceIndex : conv.Messages.Count);

                _ = _context.ConversationProviderSessions.Add(session);
                _ = await _context.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
