using System.Net;
using System.Net.Http.Json;
using AIAgentHub.Application.Conversations;
using AIAgentHub.Application.Execution;
using AIAgentHub.Application.FileChanges;
using AIAgentHub.Application.Workspaces;
using AIAgentHub.Domain.FileChanges;
using AIAgentHub.Domain.Providers;
using AIAgentHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AIAgentHub.Integration.Tests;

public sealed class IntegrationTestFactory : WebApplicationFactory<Program>
{
    private readonly string _testDbPath = Path.Combine(Path.GetTempPath(), "AgentHubTest_" + Guid.NewGuid().ToString("N") + ".db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            var descriptors = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<AgentHubDbContext>) ||
                d.ServiceType == typeof(DbContextOptions) ||
                d.ServiceType == typeof(AgentHubDbContext)).ToList();

            foreach (var d in descriptors)
                services.Remove(d);

            services.AddDbContext<AgentHubDbContext>(options =>
            {
                options.UseSqlite($"Data Source={_testDbPath}")
                       .EnableSensitiveDataLogging()
                       .LogTo(Console.WriteLine, Microsoft.Extensions.Logging.LogLevel.Information);
            });
        });
    }

    public void InitializeDatabase()
    {
        using var scope = Services.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
        initializer.InitializeAsync().GetAwaiter().GetResult();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }
}

public sealed class IntegrationTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;
    private static readonly System.Text.Json.JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public IntegrationTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _factory.InitializeDatabase();
    }

    [Fact]
    public async Task Complete_Workspace_Lifecycle_And_Deletion_Safety_Test()
    {
        var client = _factory.CreateClient();

        // 1. Check Setup Status
        var setupStatusRes = await client.GetAsync("/api/v1/auth/setup/status");
        Assert.Equal(HttpStatusCode.OK, setupStatusRes.StatusCode);

        // 2. Initialize Setup
        var setupInitRes = await client.PostAsJsonAsync("/api/v1/auth/setup/initialize", new
        {
            Username = "admin",
            Password = "SecurePassword123!",
            ConfirmPassword = "SecurePassword123!"
        });
        Assert.True(setupInitRes.IsSuccessStatusCode);

        // 3. Login
        var loginRes = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Username = "admin",
            Password = "SecurePassword123!"
        });
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);

        // 4. Create Workspace in a safe temporary directory
        var tempFolder = Path.Combine(Path.GetTempPath(), "AgentHubTestWorkspace_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);
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
            var createdWs = await createWsRes.Content.ReadFromJsonAsync<WorkspaceDto>(_jsonOptions);
            Assert.NotNull(createdWs);

            // 5. Query Workspaces
            var listRes = await client.GetAsync("/api/v1/workspaces");
            Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
            var workspaces = await listRes.Content.ReadFromJsonAsync<List<WorkspaceDto>>(_jsonOptions);
            Assert.NotNull(workspaces);
            Assert.Contains(workspaces, w => w.Id == createdWs.Id);

            // 6. Delete Workspace from AgentHub
            var deleteWsRes = await client.DeleteAsync($"/api/v1/workspaces/{createdWs.Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleteWsRes.StatusCode);

            // 7. Verify folder and files on disk are completely preserved (NOT deleted)
            Assert.True(Directory.Exists(tempFolder), "Workspace folder must NOT be deleted from disk");
            Assert.True(File.Exists(sampleFile), "Files inside workspace must NOT be deleted from disk");
            Assert.Equal("important user code", await File.ReadAllTextAsync(sampleFile));
        }
        finally
        {
            if (Directory.Exists(tempFolder))
                Directory.Delete(tempFolder, true);
        }
    }

    [Fact]
    public async Task Provider_Discovery_And_Model_Listing_Test()
    {
        var client = _factory.CreateClient();

        // 1. Providers endpoint
        var providersRes = await client.GetAsync("/api/v1/providers");
        Assert.Equal(HttpStatusCode.OK, providersRes.StatusCode);
        var providers = await providersRes.Content.ReadFromJsonAsync<List<ProviderInfo>>(_jsonOptions);
        Assert.NotNull(providers);

        // Verify Antigravity provider is present with its models
        var agyProvider = providers.FirstOrDefault(p => p.Id == "antigravity");
        Assert.NotNull(agyProvider);
        Assert.Equal("Antigravity CLI (agy)", agyProvider.DisplayName);
        Assert.NotEmpty(agyProvider.SupportedModels);
    }

    [Fact(Skip = "disabling antigravity test for now")]
    public async Task EndToEnd_Antigravity_Execution_And_FileChange_Detection_Test()
    {
        var client = _factory.CreateClient();

        // Ensure user is logged in
        await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Username = "admin",
            Password = "SecurePassword123!"
        });

        // Setup test workspace in safe folder
        var tempFolder = Path.Combine(Path.GetTempPath(), "AgentHubAgyTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        try
        {
            // 1. Create Workspace with Antigravity provider
            var createWsRes = await client.PostAsJsonAsync("/api/v1/workspaces", new
            {
                Name = "AgyExecutionTestWs",
                Path = tempFolder,
                DefaultProviderId = "antigravity"
            });
            var ws = await createWsRes.Content.ReadFromJsonAsync<WorkspaceDto>(_jsonOptions);
            Assert.NotNull(ws);

            // 2. Create Conversation
            var createConvRes = await client.PostAsJsonAsync("/api/v1/conversations", new
            {
                WorkspaceId = ws.Id,
                Title = "Antigravity Automated Test",
                ProviderId = "antigravity",
                ModelId = "Gemini 3.6 Flash (High)"
            });
            Assert.Equal(HttpStatusCode.Created, createConvRes.StatusCode);
            var conv = await createConvRes.Content.ReadFromJsonAsync<ConversationDto>(_jsonOptions);
            Assert.NotNull(conv);

            // 3. Directly run Orchestrator execution with Antigravity
            using var scope = _factory.Services.CreateScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<IExecutionOrchestrator>();
            var snapshotService = scope.ServiceProvider.GetRequiredService<ISnapshotService>();

            // Run execution orchestrator prompt with real Antigravity provider
            await orchestrator.ExecuteAsync(conv.Id, "Hello Antigravity!");

            // 4. Verify conversation messages
            var convDetailsRes = await client.GetAsync($"/api/v1/conversations/{conv.Id}");
            Assert.Equal(HttpStatusCode.OK, convDetailsRes.StatusCode);
            var updatedConv = await convDetailsRes.Content.ReadFromJsonAsync<ConversationDetailDto>(_jsonOptions);
            Assert.NotNull(updatedConv);

            // User prompt and Assistant response are both present and populated
            Assert.True(updatedConv.Messages.Count >= 2);
            Assert.Equal("Hello Antigravity!", updatedConv.Messages[0].Content);
            Assert.False(string.IsNullOrWhiteSpace(updatedConv.Messages[1].Content));

            // 5. Verify snapshot lifecycle detects created and modified files
            var token = await snapshotService.CaptureWorkspaceSnapshotAsync(ws.Id, conv.Id, tempFolder, Array.Empty<string>());
            
            var testFilePath = Path.Combine(tempFolder, "hello_agent.txt");
            await File.WriteAllTextAsync(testFilePath, "Hello from Antigravity Agent Hub test!");

            var createdChanges = await snapshotService.DetectAndRecordChangesAsync(ws.Id, conv.Id, tempFolder, token, Array.Empty<string>());
            Assert.Single(createdChanges);
            Assert.Equal(FileChangeType.Created, createdChanges[0].ChangeType);

            // 6. Test Modifying the same file
            var token2 = await snapshotService.CaptureWorkspaceSnapshotAsync(ws.Id, conv.Id, tempFolder, Array.Empty<string>());
            await File.WriteAllTextAsync(testFilePath, "Updated line 2 in file!");

            var modifiedChanges = await snapshotService.DetectAndRecordChangesAsync(ws.Id, conv.Id, tempFolder, token2, Array.Empty<string>());
            Assert.Single(modifiedChanges);
            Assert.Equal(FileChangeType.Modified, modifiedChanges[0].ChangeType);
        }
        finally
        {
            if (Directory.Exists(tempFolder))
            {
                try { Directory.Delete(tempFolder, true); } catch { }
            }
        }
    }
}
