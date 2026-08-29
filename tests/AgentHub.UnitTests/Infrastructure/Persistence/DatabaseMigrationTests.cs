using AIAgentHub.Domain.Conversations;
using AIAgentHub.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgentHub.UnitTests.Infrastructure.Persistence;

public sealed class DatabaseMigrationTests
{
    [Fact]
    public void DatabaseModel_ShouldHaveNoPendingModelChanges()
    {
        var options = new DbContextOptionsBuilder<AgentHubDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var db = new AgentHubDbContext(options);
        var hasPendingModelChanges = db.Database.HasPendingModelChanges();

        Assert.False(hasPendingModelChanges, "The C# DbContext model has diverged from the migrations or ModelSnapshot.");
    }

    [Fact]
    public async Task DatabaseMigrations_ShouldApplyCleanlyFromScratch()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"MigrationScratchTest_{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AgentHubDbContext>()
            .UseSqlite($"Data Source={tempDb}")
            .Options;

        try
        {
            using var db = new AgentHubDbContext(options);
            await db.Database.MigrateAsync();

            var tables = await db.Database.SqlQueryRaw<string>(
                "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name NOT LIKE '__EFMigrationsHistory';")
                .ToListAsync();

            Assert.Contains("Workspaces", tables);
            Assert.Contains("Conversations", tables);
            Assert.Contains("Messages", tables);
            Assert.Contains("FileChanges", tables);
            Assert.Contains("FileSnapshots", tables);
            Assert.Contains("Users", tables);
            Assert.Contains("ServerSettings", tables);
            Assert.Contains("Secrets", tables);
            Assert.Contains("Skills", tables);
            Assert.Contains("McpServers", tables);
            Assert.Contains("PermissionRequests", tables);
            Assert.Contains("ProviderModelSettings", tables);
            Assert.Contains("ProviderDetectionRecords", tables);
            Assert.Contains("ConversationProviderSessions", tables);
        }
        finally
        {
            if (File.Exists(tempDb))
            {
                try { File.Delete(tempDb); } catch { }
            }
        }
    }

    [Fact]
    public async Task DatabaseMigrations_UpgradingFrom_v0_1_To_v0_2_ShouldSucceedAndPreserveLegacyData()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"MigrationUpgradeTest_{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AgentHubDbContext>()
            .UseSqlite($"Data Source={tempDb}")
            .Options;

        try
        {
            var workspaceId = Guid.NewGuid();
            var conversationId = Guid.NewGuid();
            var message1Id = Guid.NewGuid();
            var message2Id = Guid.NewGuid();

            using (var db = new AgentHubDbContext(options))
            {
                var migrator = db.GetService<IMigrator>();

                // 1. Migrate ONLY to the v0.1 baseline (simulating a v0.1 database)
                await migrator.MigrateAsync("20260810172914_InitialCreate");

                // 2. Insert v0.1 legacy records via raw SQL (v0.1 schema without v0.2 columns)
                var connection = (Microsoft.Data.Sqlite.SqliteConnection)db.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                {
                    await connection.OpenAsync();
                }

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO Workspaces (Id, Name, Path, Origin, CreatedAtUtc, LastAccessedAtUtc, Settings_IgnoredFiles, Settings_AutoAcceptDiffs)
                    VALUES ($wsId, 'Legacy Workspace', 'C:\legacy\path', 0, '2026-08-10 12:00:00+00:00', '2026-08-10 12:00:00+00:00', '.git;bin', 0);

                    INSERT INTO Conversations (Id, WorkspaceId, Title, ProviderId, ModelId, Effort, ProviderSessionId, CreatedAtUtc, UpdatedAtUtc, LastUserInteractionAtUtc)
                    VALUES ($cId, $wsId, 'Legacy Conversation', 'gemini', 'gemini-2.5-pro', 'high', 'session-123', '2026-08-10 12:05:00+00:00', '2026-08-10 12:05:00+00:00', '2026-08-10 12:05:00+00:00');

                    INSERT INTO Messages (Id, ConversationId, Role, Content, CreatedAtUtc, Metadata)
                    VALUES ($m1Id, $cId, 0, 'First user message', '2026-08-10 12:06:00+00:00', NULL);

                    INSERT INTO Messages (Id, ConversationId, Role, Content, CreatedAtUtc, Metadata)
                    VALUES ($m2Id, $cId, 1, 'First assistant response', '2026-08-10 12:07:00+00:00', NULL);
                ";
                cmd.Parameters.AddWithValue("$wsId", workspaceId);
                cmd.Parameters.AddWithValue("$cId", conversationId);
                cmd.Parameters.AddWithValue("$m1Id", message1Id);
                cmd.Parameters.AddWithValue("$m2Id", message2Id);
                _ = await cmd.ExecuteNonQueryAsync();

                // 3. Apply the v0.2 migration on top of the legacy database
                await migrator.MigrateAsync("20260828160023_v0_2_0_AddVersion02MultiProviderTracking");
            }

            // 4. Run data healing using a fresh DbContext instance with the updated v0.2 schema
            using (var db = new AgentHubDbContext(options))
            {
                var settingsRepo = new ServerSettingsRepository(db);
                var initializer = new DatabaseInitializer(db, settingsRepo);
                await initializer.InitializeAsync();
            }

            // 5. Query using standard EF Core entity models in a fresh DbContext instance
            using (var db = new AgentHubDbContext(options))
            {
                var workspace = await db.Workspaces.FindAsync(workspaceId);
                Assert.NotNull(workspace);
                Assert.Equal("Legacy Workspace", workspace.Name);
                Assert.False(workspace.IsFavorite);
                Assert.False(workspace.IsArchived);

                var conversation = await db.Conversations
                    .Include(c => c.Messages)
                    .Include(c => c.ProviderSessions)
                    .FirstOrDefaultAsync(c => c.Id == conversationId);

                Assert.NotNull(conversation);
                Assert.Equal("Legacy Conversation", conversation.Title);
                Assert.Equal(ConversationStatus.Active, conversation.Status);
                Assert.False(conversation.IsPinned);

                // Messages should be healed with sequence index 1 and 2
                Assert.Equal(2, conversation.Messages.Count);
                var messages = conversation.Messages.OrderBy(m => m.CreatedAtUtc).ToList();
                Assert.Equal(1, messages[0].SequenceIndex);
                Assert.Equal("gemini", messages[0].OriginProviderId);
                Assert.Equal(2, messages[1].SequenceIndex);
                Assert.Equal("gemini", messages[1].OriginProviderId);

                // Initial provider session should be automatically created
                Assert.Single(conversation.ProviderSessions);
                var session = conversation.ProviderSessions.First();
                Assert.Equal("gemini", session.ProviderId);
                Assert.Equal("session-123", session.ProviderSessionId);
                Assert.Equal(2, session.LastSharedSequenceIndex);

                // Adding a new v0.2 message with ExecutionMetadata should succeed seamlessly
                var metadata = new ExecutionMetadata
                {
                    ProviderId = "anthropic",
                    ModelId = "claude-3-5-sonnet",
                    Tokens = 300,
                    DurationMs = 1200,
                    IsSuccess = true
                };

                var newMsg = conversation.AddMessage(
                    MessageRole.Assistant,
                    "New Claude response after provider switch",
                    metadata,
                    originProviderId: "anthropic",
                    originModelId: "claude-3-5-sonnet");

                var convRepo = new ConversationRepository(db);
                await convRepo.UpdateAsync(conversation);

                var savedMsg = await db.Messages.FindAsync(newMsg.Id);
                Assert.NotNull(savedMsg);
                Assert.Equal("anthropic", savedMsg.OriginProviderId);
                Assert.Equal("claude-3-5-sonnet", savedMsg.OriginModelId);
                Assert.NotNull(savedMsg.Metadata);
                Assert.Equal(300, savedMsg.Metadata.Tokens);
            }
        }
        finally
        {
            if (File.Exists(tempDb))
            {
                try { File.Delete(tempDb); } catch { }
            }
        }
    }
}
