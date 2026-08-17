using AIAgentHub.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace AgentHub.UnitTests.Infrastructure.Persistence;

public sealed class ProviderModelSettingRepositoryTests
{
    [Fact]
    public async Task ProviderModelSettingRepository_Reconcile_ShouldPreserveSettingsDeleteObsoleteAndAddNewAsEnabled()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), "ProviderModelSettingTestDb_" + Guid.NewGuid().ToString("N") + ".db");
        var options = new DbContextOptionsBuilder<AgentHubDbContext>()
            .UseSqlite($"Data Source={tempDb}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        try
        {
            using var db = new AgentHubDbContext(options);
            _ = await db.Database.EnsureCreatedAsync();
            var repo = new ProviderModelSettingRepository(db);

            var providerId = "opencode";
            var initialModels = new List<AIAgentHub.Domain.Providers.ModelInfo>
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
            var refreshedModels = new List<AIAgentHub.Domain.Providers.ModelInfo>
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
}
