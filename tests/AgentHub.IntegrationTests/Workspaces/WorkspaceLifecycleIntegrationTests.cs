using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentHub.IntegrationTests.Web;
using AIAgentHub.Application.Conversations;
using AIAgentHub.Application.Execution;
using AIAgentHub.Application.FileChanges;
using AIAgentHub.Application.Workspaces;
using AIAgentHub.Domain.FileChanges;
using AIAgentHub.Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgentHub.IntegrationTests.Workspaces;

public sealed class WorkspaceLifecycleIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public WorkspaceLifecycleIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.InitializeDatabase();
    }

    [Fact]
    public async Task Complete_Workspace_Lifecycle_And_Deletion_Safety_Test()
    {
        var client = await CreateAuthenticatedClientAsync();

        // 1. Create Workspace in a safe temporary directory
        var tempFolder = Path.Combine(Path.GetTempPath(), "AgentHubTestWorkspace_" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(tempFolder);
        var sampleFile = Path.Combine(tempFolder, "sample.txt");
        await File.WriteAllTextAsync(sampleFile, "important user code");

        try
        {
            var createWsRes = await client.PostAsJsonAsync("/api/v1/workspaces", new
            {
                Name = "IntegrationTestWs",
                Path = tempFolder,
                DefaultProviderId = "antigravity"
            });
            Assert.Equal(HttpStatusCode.Created, createWsRes.StatusCode);
            var createdWs = await createWsRes.Content.ReadFromJsonAsync<WorkspaceDto>(JsonOpts);
            Assert.NotNull(createdWs);

            // 2. Query Workspaces
            var listRes = await client.GetAsync("/api/v1/workspaces");
            Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
            var workspaces = await listRes.Content.ReadFromJsonAsync<List<WorkspaceDto>>(JsonOpts);
            Assert.NotNull(workspaces);
            Assert.Contains(workspaces, w => w.Id == createdWs.Id);

            // 3. Delete Workspace from AgentHub
            var deleteWsRes = await client.DeleteAsync($"/api/v1/workspaces/{createdWs.Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleteWsRes.StatusCode);

            // 4. Verify folder and files on disk are completely preserved (NOT deleted)
            Assert.True(Directory.Exists(tempFolder), "Workspace folder must NOT be deleted from disk");
            Assert.True(File.Exists(sampleFile), "Files inside workspace must NOT be deleted from disk");
            Assert.Equal("important user code", await File.ReadAllTextAsync(sampleFile));
        }
        finally
        {
            if (Directory.Exists(tempFolder))
            {
                try { Directory.Delete(tempFolder, true); } catch { }
            }
        }
    }

    [Fact]
    public async Task Provider_Discovery_And_Model_Listing_Test()
    {
        var client = await CreateAuthenticatedClientAsync();

        // 1. Providers endpoint
        var providersRes = await client.GetAsync("/api/v1/providers");
        Assert.Equal(HttpStatusCode.OK, providersRes.StatusCode);
        var providers = await providersRes.Content.ReadFromJsonAsync<List<ProviderInfo>>(JsonOpts);
        Assert.NotNull(providers);

        // Verify Antigravity provider is present with its models
        var agyProvider = providers.FirstOrDefault(p => p.Id == "antigravity");
        Assert.NotNull(agyProvider);
        Assert.Equal("Antigravity CLI", agyProvider.DisplayName);
        Assert.NotEmpty(agyProvider.SupportedModels);
    }

    [Fact]
    public async Task EndToEnd_Antigravity_Execution_And_FileChange_Detection_Test()
    {
        var client = await CreateAuthenticatedClientAsync();

        // Setup test workspace in safe folder
        var tempFolder = Path.Combine(Path.GetTempPath(), "AgentHubAgyTest_" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(tempFolder);

        try
        {
            // 1. Create Workspace with Antigravity provider
            var createWsRes = await client.PostAsJsonAsync("/api/v1/workspaces", new
            {
                Name = "AgyExecutionTestWs",
                Path = tempFolder,
                DefaultProviderId = "antigravity"
            });
            var ws = await createWsRes.Content.ReadFromJsonAsync<WorkspaceDto>(JsonOpts);
            Assert.NotNull(ws);

            // 2. Create Conversation
            var createConvRes = await client.PostAsJsonAsync("/api/v1/conversations", new
            {
                WorkspaceId = ws.Id,
                Title = "Antigravity Automated Test",
                ProviderId = "antigravity",
                ModelId = "default"
            });
            Assert.Equal(HttpStatusCode.Created, createConvRes.StatusCode);
            var conv = await createConvRes.Content.ReadFromJsonAsync<ConversationDto>(JsonOpts);
            Assert.NotNull(conv);

            // 3. Directly run Orchestrator execution with Antigravity
            using var scope = _factory.Services.CreateScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<IExecutionOrchestrator>();
            var snapshotService = scope.ServiceProvider.GetRequiredService<ISnapshotService>();

            // Run execution orchestrator prompt
            await orchestrator.ExecuteAsync(conv.Id, "Hello Antigravity!");

            // 4. Verify conversation messages
            var convDetailsRes = await client.GetAsync($"/api/v1/conversations/{conv.Id}");
            Assert.Equal(HttpStatusCode.OK, convDetailsRes.StatusCode);
            var updatedConv = await convDetailsRes.Content.ReadFromJsonAsync<ConversationDetailDto>(JsonOpts);
            Assert.NotNull(updatedConv);

            // User prompt and Assistant response are both present and populated
            Assert.True(updatedConv.Messages.Count >= 2);
            Assert.Equal("Hello Antigravity!", updatedConv.Messages[0].Content);
            Assert.False(string.IsNullOrWhiteSpace(updatedConv.Messages[1].Content));

            // 5. Verify snapshot lifecycle detects created and modified files
            var baselineFilePath = Path.Combine(tempFolder, "existing_code.txt");
            await File.WriteAllTextAsync(baselineFilePath, "Original baseline content.");

            var token = await snapshotService.CaptureWorkspaceSnapshotAsync(ws.Id, conv.Id, tempFolder, Array.Empty<string>());

            var newFilePath = Path.Combine(tempFolder, "hello_agent.txt");
            await File.WriteAllTextAsync(newFilePath, "Hello from Antigravity Agent Hub test!");
            await File.WriteAllTextAsync(baselineFilePath, "Updated baseline content!");

            var changes = await snapshotService.DetectAndRecordChangesAsync(ws.Id, conv.Id, tempFolder, token, Array.Empty<string>());
            Assert.Equal(2, changes.Count);

            var createdChange = changes.FirstOrDefault(c => c.RelativePath.Replace('\\', '/') == "hello_agent.txt");
            Assert.NotNull(createdChange);
            Assert.Equal(FileChangeType.Created, createdChange.ChangeType);

            var modifiedChange = changes.FirstOrDefault(c => c.RelativePath.Replace('\\', '/') == "existing_code.txt");
            Assert.NotNull(modifiedChange);
            Assert.Equal(FileChangeType.Modified, modifiedChange.ChangeType);
        }
        finally
        {
            if (Directory.Exists(tempFolder))
            {
                try { Directory.Delete(tempFolder, true); } catch { }
            }
        }
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = _factory.CreateClient();
        var setupStatusRes = await client.GetAsync("/api/v1/auth/setup/status");
        var setupStatus = await setupStatusRes.Content.ReadFromJsonAsync<SetupStatusResponse>(JsonOpts);

        if (setupStatus?.IsSetupCompleted != true)
        {
            _ = await client.PostAsJsonAsync("/api/v1/auth/setup/initialize", new
            {
                Username = "admin",
                Password = "SecurePassword123!",
                ConfirmPassword = "SecurePassword123!"
            });
        }

        _ = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Username = "admin",
            Password = "SecurePassword123!"
        });
        return client;
    }

    private sealed record SetupStatusResponse(bool IsSetupCompleted, bool IsRecoveryModeEnabled, bool IsLocalRequest, bool CanResetWithoutCode);
}
