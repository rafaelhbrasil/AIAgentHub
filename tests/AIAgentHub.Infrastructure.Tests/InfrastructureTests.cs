using AIAgentHub.Domain.FileChanges;
using AIAgentHub.Domain.Repositories;
using AIAgentHub.Infrastructure.Cryptography;
using AIAgentHub.Infrastructure.Snapshots;

using Microsoft.EntityFrameworkCore;

namespace AIAgentHub.Infrastructure.Tests;

public sealed class InfrastructureTests
{
    [Fact]
    public void Argon2id_HashAndVerifyPassword_ShouldSucceed()
    {
        var hasher = new Argon2idPasswordHasher();
        var (hash, salt) = hasher.HashPassword("SuperSecretPassword123!");

        Assert.NotEmpty(hash);
        Assert.NotEmpty(salt);

        var isValid = hasher.VerifyPassword("SuperSecretPassword123!", hash, salt);
        Assert.True(isValid);

        var isInvalid = hasher.VerifyPassword("WrongPassword", hash, salt);
        Assert.False(isInvalid);
    }

    [Fact]
    public void Argon2id_GenerateRecoveryCode_ShouldFormatCorrectly()
    {
        var hasher = new Argon2idPasswordHasher();
        var (hash, plainCode) = hasher.GenerateRecoveryCode();

        Assert.NotEmpty(hash);
        Assert.NotEmpty(plainCode);
        Assert.Contains("-", plainCode);
    }

    [Fact]
    public void AesGcm_EncryptAndDecrypt_ShouldRestoreOriginalSecret()
    {
        var keyProvider = new MasterKeyProvider();
        var encryptor = new AesGcmSecretEncryptor(keyProvider);

        var originalSecret = "sk-ant-api03-abcdef123456789";
        var (ciphertext, nonce, tag) = encryptor.Encrypt(originalSecret);

        Assert.NotEmpty(ciphertext);
        Assert.NotEmpty(nonce);
        Assert.NotEmpty(tag);

        var decrypted = encryptor.Decrypt(ciphertext, nonce, tag);
        Assert.Equal(originalSecret, decrypted);
    }

    [Fact]
    public async Task SnapshotStore_Rollback_ShouldRestoreModifiedFile()
    {
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "AgentHubTestWs_" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(tempWorkspace);
        var testFile = Path.Combine(tempWorkspace, "test.txt");

        try
        {
            await File.WriteAllTextAsync(testFile, "Baseline Original Content");

            var fakeSnapshots = new FakeSnapshotRepo();
            var fakeChanges = new FakeChangeRepo();
            var store = new LocalDiskSnapshotStore(fakeSnapshots, fakeChanges);

            var wsId = Guid.NewGuid();
            var convId = Guid.NewGuid();

            var token = await store.CaptureWorkspaceSnapshotAsync(wsId, convId, tempWorkspace, Array.Empty<string>());
            Assert.NotEmpty(token);

            // Simulate file modification by AI
            await File.WriteAllTextAsync(testFile, "Modified Content by AI");

            var detected = await store.DetectAndRecordChangesAsync(wsId, convId, tempWorkspace, token, Array.Empty<string>());
            _ = Assert.Single(detected);
            Assert.Equal(FileChangeType.Modified, detected[0].ChangeType);

            // Test Reject & Rollback
            await store.RollbackFileAsync(detected[0], tempWorkspace);

            var rolledBackContent = await File.ReadAllTextAsync(testFile);
            Assert.Equal("Baseline Original Content", rolledBackContent);
        }
        finally
        {
            if (Directory.Exists(tempWorkspace))
            {
                Directory.Delete(tempWorkspace, true);
            }
        }
    }

    private sealed class FakeSnapshotRepo : IFileSnapshotRepository
    {
        private readonly List<FileSnapshot> _list = [];
        public Task AddAsync(FileSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            _list.Add(snapshot);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<FileSnapshot>> GetByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FileSnapshot>>(_list.Where(s => s.ConversationId == conversationId).ToList());
        public Task<FileSnapshot?> GetLatestByPathAsync(Guid workspaceId, string relativePath, CancellationToken cancellationToken = default) => Task.FromResult(_list.LastOrDefault(s => s.WorkspaceId == workspaceId && s.RelativePath == relativePath));
    }

    private sealed class FakeChangeRepo : IFileChangeRepository
    {
        private readonly List<FileChange> _list = [];
        public Task AddAsync(FileChange change, CancellationToken cancellationToken = default)
        {
            _list.Add(change);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<FileChange>> GetByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FileChange>>(_list.Where(c => c.ConversationId == conversationId).ToList());
        public Task<FileChange?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(_list.FirstOrDefault(c => c.Id == id));
        public Task UpdateAsync(FileChange change, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task ProviderModelSettingRepository_Reconcile_ShouldPreserveSettingsDeleteObsoleteAndAddNewAsEnabled()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), "ProviderModelSettingTestDb_" + Guid.NewGuid().ToString("N") + ".db");
        var options = new DbContextOptionsBuilder<Persistence.AgentHubDbContext>()
            .UseSqlite($"Data Source={tempDb}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        try
        {
            using var db = new Persistence.AgentHubDbContext(options);
            _ = await db.Database.EnsureCreatedAsync();
            var repo = new Persistence.ProviderModelSettingRepository(db);

            var providerId = "opencode";
            var initialModels = new List<Domain.Providers.ModelInfo>
                {
                    new() { Id = "model-1", DisplayName = "Model 1" },
                    new() { Id = "model-2", DisplayName = "Model 2" },
                    new() { Id = "model-3", DisplayName = "Model 3 (Obsolete)" }
                };

            // 1. Initial reconciliation - all inserted as IsDisplayed = true
            await repo.ReconcileAsync(providerId, initialModels);
            var settingsAfterStep1 = await repo.GetByProviderIdAsync(providerId);
            Assert.Equal(3, settingsAfterStep1.Count);
            Assert.All(settingsAfterStep1, s => Assert.True(s.IsDisplayed));

            // 2. User toggles model-1 to OFF
            await repo.UpdateSettingsAsync(providerId, new Dictionary<string, bool> { { "model-1", false } });

            // 3. Provider refreshes models: model-3 is gone, model-4 is newly added
            var refreshedModels = new List<Domain.Providers.ModelInfo>
                {
                    new() { Id = "model-1", DisplayName = "Model 1" },
                    new() { Id = "model-2", DisplayName = "Model 2" },
                    new() { Id = "model-4", DisplayName = "Model 4 (New)" }
                };

            await repo.ReconcileAsync(providerId, refreshedModels);

            // Verify reconciliation results
            Assert.False(refreshedModels.First(m => m.Id == "model-1").IsDisplayed); // Preserved OFF
            Assert.True(refreshedModels.First(m => m.Id == "model-2").IsDisplayed);  // Preserved ON
            Assert.True(refreshedModels.First(m => m.Id == "model-4").IsDisplayed);  // New default ON

            var finalSettings = await repo.GetByProviderIdAsync(providerId);
            Assert.Equal(3, finalSettings.Count);
            Assert.DoesNotContain(finalSettings, s => s.ModelId == "model-3"); // Obsolete deleted
            Assert.False(finalSettings.First(s => s.ModelId == "model-1").IsDisplayed);
            Assert.True(finalSettings.First(s => s.ModelId == "model-4").IsDisplayed);
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

    [Fact]
    public async Task ConversationRepository_GetByWorkspaceIdAsync_WithNullModelId_ShouldSucceed()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"AgentHubTestConv_{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<Persistence.AgentHubDbContext>()
            .UseSqlite($"Data Source={tempDb}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        try
        {
            using var db = new Persistence.AgentHubDbContext(options);
            _ = await db.Database.EnsureCreatedAsync();

            var ws = Domain.Workspaces.Workspace.Create("OpenCode Workspace", "C:\\test\\ws");
            _ = db.Workspaces.Add(ws);
            _ = await db.SaveChangesAsync();

            // Create conversation with null modelId and null providerSessionId (as in OpenCode)
            var conv = Domain.Conversations.Conversation.Create(ws.Id, "OpenCode Session", "opencode", modelId: null, providerSessionId: null);
            _ = conv.AddMessage(Domain.Conversations.MessageRole.User, "Hello OpenCode");
            _ = db.Conversations.Add(conv);
            _ = await db.SaveChangesAsync();

            var repo = new Persistence.ConversationRepository(db);
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
