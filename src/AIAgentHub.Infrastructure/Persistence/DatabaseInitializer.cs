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
        if (!await NeedsMessageHealingAsync(cancellationToken) && !await NeedsSessionHealingAsync(cancellationToken))
        {
            return;
        }

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
                var needsOriginUpdate = NeedsOriginBackfill(msg.OriginProviderId, conv.ProviderId);

                await HealMessageAsync(msg.Id, conv.ProviderId, conv.ModelId, expectedIndex, needsIndexUpdate, needsOriginUpdate, cancellationToken);

                lastMsgId = msg.Id;
                lastMsgSequenceIndex = expectedIndex;
            }

            if (NeedsProviderSessionBackfill(conv.ProviderId, conv.HasProviderSession))
            {
                await BackfillProviderSessionAsync(conv.Id, conv.ProviderId, conv.ProviderSessionId, lastMsgId, lastMsgSequenceIndex, conv.Messages.Count, cancellationToken);
            }
        }
    }

    private async Task<bool> NeedsMessageHealingAsync(CancellationToken cancellationToken)
    {
        return await _context.Messages
            .AnyAsync(m => m.SequenceIndex == 0 || string.IsNullOrEmpty(m.OriginProviderId), cancellationToken);
    }

    private async Task<bool> NeedsSessionHealingAsync(CancellationToken cancellationToken)
    {
        return await _context.Conversations
            .AnyAsync(c => !string.IsNullOrEmpty(c.ProviderId) && !c.ProviderSessions.Any(), cancellationToken);
    }

    private static bool NeedsOriginBackfill(string? originProviderId, string? providerId) => string.IsNullOrEmpty(originProviderId) && !string.IsNullOrEmpty(providerId);

    private static bool NeedsProviderSessionBackfill(string? providerId, bool hasProviderSession) => !string.IsNullOrEmpty(providerId) && !hasProviderSession;

    private async Task HealMessageAsync(
        Guid messageId,
        string? providerId,
        string? modelId,
        int expectedIndex,
        bool needsIndexUpdate,
        bool needsOriginUpdate,
        CancellationToken cancellationToken)
    {
        if (needsIndexUpdate && needsOriginUpdate)
        {
            _ = await _context.Messages
                .Where(m => m.Id == messageId)
                .ExecuteUpdateAsync(setter => setter
                    .SetProperty(m => m.SequenceIndex, expectedIndex)
                    .SetProperty(m => m.OriginProviderId, providerId)
                    .SetProperty(m => m.OriginModelId, modelId), cancellationToken);
        }
        else if (needsIndexUpdate)
        {
            _ = await _context.Messages
                .Where(m => m.Id == messageId)
                .ExecuteUpdateAsync(setter => setter
                    .SetProperty(m => m.SequenceIndex, expectedIndex), cancellationToken);
        }
        else if (needsOriginUpdate)
        {
            _ = await _context.Messages
                .Where(m => m.Id == messageId)
                .ExecuteUpdateAsync(setter => setter
                    .SetProperty(m => m.OriginProviderId, providerId)
                    .SetProperty(m => m.OriginModelId, modelId), cancellationToken);
        }
    }

    private async Task BackfillProviderSessionAsync(
        Guid conversationId,
        string providerId,
        string? providerSessionId,
        Guid? lastMsgId,
        int lastMsgSequenceIndex,
        int messageCount,
        CancellationToken cancellationToken)
    {
        var session = ConversationProviderSession.Create(
            conversationId,
            providerId,
            providerSessionId,
            lastMsgId,
            lastMsgSequenceIndex > 0 ? lastMsgSequenceIndex : messageCount);

        _ = _context.ConversationProviderSessions.Add(session);
        _ = await _context.SaveChangesAsync(cancellationToken);
    }
}
