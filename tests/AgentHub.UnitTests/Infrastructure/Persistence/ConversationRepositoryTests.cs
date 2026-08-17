using AIAgentHub.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace AgentHub.UnitTests.Infrastructure.Persistence;

public sealed class ConversationRepositoryTests
{
    [Fact]
    public async Task ConversationRepository_GetByWorkspaceIdAsync_WithNullModelId_ShouldSucceed()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"AgentHubTestConv_{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AgentHubDbContext>()
            .UseSqlite($"Data Source={tempDb}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        try
        {
            using var db = new AgentHubDbContext(options);
            _ = await db.Database.EnsureCreatedAsync();

            var ws = AIAgentHub.Domain.Workspaces.Workspace.Create("OpenCode Workspace", "C:\\test\\ws");
            _ = db.Workspaces.Add(ws);
            _ = await db.SaveChangesAsync();

            // Create conversation with null modelId and null providerSessionId (as in OpenCode)
            var conv = AIAgentHub.Domain.Conversations.Conversation.Create(ws.Id, "OpenCode Session", "opencode", modelId: null, providerSessionId: null);
            _ = conv.AddMessage(AIAgentHub.Domain.Conversations.MessageRole.User, "Hello OpenCode");
            _ = db.Conversations.Add(conv);
            _ = await db.SaveChangesAsync();

            var repo = new ConversationRepository(db);
            var result = await repo.GetByWorkspaceIdAsync(ws.Id);

            Assert.Single(result);
            Assert.Equal(conv.Id, result[0].Id);
            Assert.Null(result[0].ModelId);
            Assert.Single(result[0].Messages);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(tempDb))
            {
                try { File.Delete(tempDb); } catch { }
            }
        }
    }
}
