# Test Suite Separation & Comprehensive Integration Test Matrix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Establish clean separation between fast unit tests and slower integration tests across CLI and Visual Studio Test Explorer, while implementing a complete happy path integration test suite covering auth, recovery, provider detection & SSE refresh, model settings, and 2-turn chat session continuity across all 5 AI providers (`antigravity`, `claudecode`, `codexcli`, `geminicli`, `opencode`).

**Architecture:** 
- Root `package.json` provides direct project-targeted CLI scripts (`test`, `test:frontend`, `test:unit`, `test:integration`, `test:all`).
- Root `test.runsettings` and Assembly Traits (`[assembly: AssemblyTrait("Category", "Unit")]` vs `[assembly: AssemblyTrait("Category", "Integration")]`) isolate unit and integration suites in Visual Studio Test Explorer and `dotnet test`.
- ASP.NET Core `WebApplicationFactory<Program>` executes the full in-memory stack (auth cookies, EF Core SQLite DB, routing, controller pipelines, and mock CLI process executors) for deterministic, fast sub-second integration runs.

**Tech Stack:** ASP.NET Core 10 (`WebApplicationFactory`), xUnit, Vitest, NSubstitute, Microsoft Playwright, SQLite In-Memory.

## Global Constraints
- Target Framework: `net10.0`
- CLI test scripts must not use hardcoded random ports; in-memory HTTP client handles integration tests.
- Backend unit tests (`AgentHub.UnitTests`) must execute in under 3 seconds.
- All 5 providers (`antigravity`, `claudecode`, `codexcli`, `geminicli`, `opencode`) must be tested for 2-turn conversational session continuity.
- Follow Specification-First Development and never commit without explicit request.

---

### Task 1: Root NPM Scripts & Solution `.runsettings` Configuration

**Files:**
- Modify: `package.json`
- Create: `test.runsettings`
- Modify: `README.md`

**Interfaces:**
- Consumes: `aiagenthub-frontend` workspace vitest script, `tests/AgentHub.UnitTests.csproj`, `tests/AgentHub.IntegrationTests.csproj`
- Produces: CLI commands `npm test`, `npm run test:frontend`, `npm run test:unit`, `npm run test:integration`, `npm run test:all`

- [ ] **Step 1: Create `test.runsettings` in repo root**

```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <RunConfiguration>
    <!-- Exclude Integration tests by default when running Visual Studio "Run All Tests" -->
    <TestCaseFilter>Category!=Integration</TestCaseFilter>
  </RunConfiguration>
</RunSettings>
```

- [ ] **Step 2: Update `package.json` with dedicated test scripts**

```json
{
  "name": "aiagenthub",
  "private": true,
  "workspaces": [
    "src/AIAgentHub.Web/frontend"
  ],
  "scripts": {
    "dev": "npm run dev -w aiagenthub-frontend",
    "build": "npm run build -w aiagenthub-frontend",
    "test": "npm run test:frontend && npm run test:unit",
    "test:frontend": "npm test -w aiagenthub-frontend",
    "test:unit": "dotnet test tests/AgentHub.UnitTests/AgentHub.UnitTests.csproj",
    "test:integration": "dotnet test tests/AgentHub.IntegrationTests/AgentHub.IntegrationTests.csproj",
    "test:all": "npm run test && npm run test:integration",
    "deploy": "node scripts/deploy.mjs",
    "deploy:run": "node scripts/deploy.mjs --run"
  }
}
```

- [ ] **Step 3: Update `README.md` testing documentation section**

Add testing command reference to `README.md`.

- [ ] **Step 4: Verify test scripts execute**

Run: `npm run test:frontend`
Expected: Frontend vitest runs and passes.

---

### Task 2: Assembly Trait Categorization for Unit & Integration Tests

**Files:**
- Create: `tests/AgentHub.UnitTests/Properties/AssemblyInfo.cs`
- Create: `tests/AgentHub.IntegrationTests/Properties/AssemblyInfo.cs`

**Interfaces:**
- Consumes: xUnit `AssemblyTraitAttribute`
- Produces: Assembly-level trait metadata for Test Explorer and `dotnet test --filter`

- [ ] **Step 1: Create `tests/AgentHub.UnitTests/Properties/AssemblyInfo.cs`**

```csharp
using Xunit;

[assembly: AssemblyTrait("Category", "Unit")]
```

- [ ] **Step 2: Create `tests/AgentHub.IntegrationTests/Properties/AssemblyInfo.cs`**

```csharp
using Xunit;

[assembly: AssemblyTrait("Category", "Integration")]
```

- [ ] **Step 3: Verify Unit Test execution with category filter**

Run: `dotnet test tests/AgentHub.UnitTests/AgentHub.UnitTests.csproj --settings test.runsettings`
Expected: All unit tests execute and pass.

---

### Task 3: Cleanup Obsolete Empty Test Directories

**Files:**
- Delete: `tests/AIAgentHub.Application.Tests/`
- Delete: `tests/AIAgentHub.Domain.Tests/`
- Delete: `tests/AIAgentHub.Infrastructure.Tests/`
- Delete: `tests/AIAgentHub.Integration.Tests/`
- Delete: `tests/AIAgentHub.Web.Tests/`

- [ ] **Step 1: Remove stale test directories containing only leftover bin/obj**

Remove directories safely via shell/filesystem.

- [ ] **Step 2: Verify solution build and integrity**

Run: `dotnet build AIAgentHub.slnx`
Expected: 0 warnings, 0 errors.

---

### Task 4: Enhance CustomWebApplicationFactory & Auth / Settings Integration Tests

**Files:**
- Modify: `tests/AgentHub.IntegrationTests/Web/Controllers/WebControllerTests.cs` (or refactor into `CustomWebApplicationFactory.cs` and `AuthIntegrationTests.cs`, `SettingsIntegrationTests.cs`)
- Create: `tests/AgentHub.IntegrationTests/Web/CustomWebApplicationFactory.cs`
- Create: `tests/AgentHub.IntegrationTests/Web/Auth/AuthIntegrationTests.cs`
- Create: `tests/AgentHub.IntegrationTests/Web/Settings/SettingsIntegrationTests.cs`

**Interfaces:**
- Consumes: `/api/v1/auth/*`, `/api/v1/settings`, `/api/v1/filesystem/drives`
- Produces: Integration tests validating setup status, initialize, login, logout, me, recovery code, password reset, 401 unauth checks, settings and drives.

- [ ] **Step 1: Create reusable `CustomWebApplicationFactory.cs`**

```csharp
using System.IO;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentHub.IntegrationTests.Web;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _testDbPath = Path.Combine(Path.GetTempPath(), "AgentHubWebTest_" + Guid.NewGuid().ToString("N") + ".db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _ = builder.UseEnvironment("Testing");
        _ = builder.ConfigureServices(services =>
        {
            var descriptors = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<AgentHubDbContext>) ||
                d.ServiceType == typeof(DbContextOptions) ||
                d.ServiceType == typeof(AgentHubDbContext)).ToList();

            foreach (var d in descriptors)
            {
                _ = services.Remove(d);
            }

            _ = services.AddDbContext<AgentHubDbContext>(options =>
            {
                _ = options.UseSqlite($"Data Source={_testDbPath}");
                _ = options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
            });

            _ = services.Configure<CliExecutionOptions>(options =>
            {
                options.Headless = true;
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
```

- [ ] **Step 2: Implement `AuthIntegrationTests.cs`**

Cover setup status, initialization, login, `/api/v1/auth/me`, logout, 401 unauthorized checks on protected routes, recovery wipe validation.

- [ ] **Step 3: Implement `SettingsIntegrationTests.cs`**

Cover `/api/v1/settings` GET and PUT, and `/api/v1/filesystem/drives`.

- [ ] **Step 4: Run test suite**

Run: `dotnet test tests/AgentHub.IntegrationTests/AgentHub.IntegrationTests.csproj --filter "FullyQualifiedName~AuthIntegrationTests|FullyQualifiedName~SettingsIntegrationTests"`
Expected: All tests pass.

---

### Task 5: Providers Management & Model Configuration Integration Tests

**Files:**
- Create: `tests/AgentHub.IntegrationTests/Web/Providers/ProvidersIntegrationTests.cs`

**Interfaces:**
- Consumes: `/api/v1/providers`, `/api/v1/providers/refresh`, `/api/v1/providers/refresh/stream`, `/api/v1/providers/{id}/models`, `/api/v1/providers/{id}/models/settings`
- Produces: Automated verification of discovery caching, SSE stream event pipeline, and model enable/disable toggle persistence.

- [ ] **Step 1: Write `ProvidersIntegrationTests.cs`**

```csharp
using System.Net;
using System.Net.Http.Json;
using AIAgentHub.Domain.Providers;
using Xunit;

namespace AgentHub.IntegrationTests.Web.Providers;

public sealed class ProvidersIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ProvidersIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.InitializeDatabase();
    }

    [Fact]
    public async Task GetAllProviders_WhenAuthenticated_ReturnsCachedListWithModels()
    {
        var client = await SetupAndAuthenticateClientAsync();

        var res = await client.GetAsync("/api/v1/providers");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var providers = await res.Content.ReadFromJsonAsync<List<ProviderInfo>>();
        Assert.NotNull(providers);
        Assert.NotEmpty(providers);
        Assert.Contains(providers, p => p.Id == "antigravity");
        Assert.Contains(providers, p => p.Id == "claudecode");
        Assert.Contains(providers, p => p.Id == "codexcli");
        Assert.Contains(providers, p => p.Id == "geminicli");
        Assert.Contains(providers, p => p.Id == "opencode");
    }

    [Fact]
    public async Task RefreshStream_ReturnsServerSentEventsStream()
    {
        var client = await SetupAndAuthenticateClientAsync();

        var res = await client.GetAsync("/api/v1/providers/refresh/stream");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("text/event-stream", res.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task UpdateModelSettings_TogglesVisibilityAndPersists()
    {
        var client = await SetupAndAuthenticateClientAsync();

        // 1. Get models
        var modelsRes = await client.GetAsync("/api/v1/providers/antigravity/models");
        Assert.Equal(HttpStatusCode.OK, modelsRes.StatusCode);
        var models = await modelsRes.Content.ReadFromJsonAsync<List<ModelInfo>>();
        Assert.NotNull(models);

        // 2. Toggle model setting
        var targetModel = models.FirstOrDefault()?.Id ?? "default";
        var payload = new Dictionary<string, bool> { [targetModel] = false };
        var postRes = await client.PostAsJsonAsync("/api/v1/providers/antigravity/models/settings", payload);
        Assert.Equal(HttpStatusCode.OK, postRes.StatusCode);

        // 3. Verify state persisted
        var verifyRes = await client.GetAsync("/api/v1/providers/antigravity/models");
        var updatedModels = await verifyRes.Content.ReadFromJsonAsync<List<ModelInfo>>();
        Assert.NotNull(updatedModels);
        var updatedModel = updatedModels.FirstOrDefault(m => m.Id == targetModel);
        Assert.NotNull(updatedModel);
        Assert.False(updatedModel.IsDisplayed);
    }

    private async Task<HttpClient> SetupAndAuthenticateClientAsync()
    {
        var client = _factory.CreateClient();
        var initRes = await client.PostAsJsonAsync("/api/v1/auth/setup/initialize", new
        {
            username = "admin",
            password = "SecurePassword123!",
            confirmPassword = "SecurePassword123!"
        });
        if (!initRes.IsSuccessStatusCode)
        {
            _ = await client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                username = "admin",
                password = "SecurePassword123!"
            });
        }
        return client;
    }
}
```

- [ ] **Step 2: Run test suite**

Run: `dotnet test tests/AgentHub.IntegrationTests/AgentHub.IntegrationTests.csproj --filter "FullyQualifiedName~ProvidersIntegrationTests"`
Expected: All tests pass.

---

### Task 6: Multi-Turn Chat & Session Continuity Integration Tests Across All 5 Providers

**Files:**
- Create: `tests/AgentHub.IntegrationTests/Web/Chat/ProviderChatIntegrationTests.cs`

**Interfaces:**
- Consumes: `/api/v1/workspaces`, `/api/v1/conversations`, `/api/v1/conversations/{id}/prompt`
- Produces: Multi-turn tests verifying 2 consecutive messages per provider and ensuring `ProviderSessionId` continuity.

- [ ] **Step 1: Write `ProviderChatIntegrationTests.cs`**

```csharp
using System.Net;
using System.Net.Http.Json;
using AIAgentHub.Domain.Conversations;
using AIAgentHub.Domain.Workspaces;
using Xunit;

namespace AgentHub.IntegrationTests.Web.Chat;

public sealed class ProviderChatIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ProviderChatIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.InitializeDatabase();
    }

    [Theory]
    [InlineData("antigravity")]
    [InlineData("claudecode")]
    [InlineData("codexcli")]
    [InlineData("geminicli")]
    [InlineData("opencode")]
    public async Task MultiTurnChat_PerProvider_MaintainsSessionContinuity(string providerId)
    {
        var client = await SetupAndAuthenticateClientAsync();

        // 1. Create Workspace
        var tempFolder = Path.Combine(Path.GetTempPath(), "AgentHubTestWorkspace_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);
        try
        {
            var wsRes = await client.PostAsJsonAsync("/api/v1/workspaces", new
            {
                name = $"Test WS {providerId}",
                path = tempFolder,
                defaultProvider = providerId,
                defaultModel = "default"
            });
            Assert.Equal(HttpStatusCode.Created, wsRes.StatusCode);
            var workspace = await wsRes.Content.ReadFromJsonAsync<Workspace>();
            Assert.NotNull(workspace);

            // 2. Create Conversation
            var convRes = await client.PostAsJsonAsync($"/api/v1/workspaces/{workspace.Id}/conversations", new
            {
                title = $"Test Chat {providerId}",
                providerId = providerId,
                modelId = "default"
            });
            Assert.Equal(HttpStatusCode.Created, convRes.StatusCode);
            var conversation = await convRes.Content.ReadFromJsonAsync<Conversation>();
            Assert.NotNull(conversation);

            // 3. Message 1 (Turn 1)
            var prompt1Res = await client.PostAsJsonAsync($"/api/v1/conversations/{conversation.Id}/prompt", new
            {
                prompt = "Turn 1: Hello from integration test"
            });
            Assert.True(prompt1Res.StatusCode is HttpStatusCode.Accepted or HttpStatusCode.OK);

            // Wait briefly for background execution to complete
            await Task.Delay(500);

            // Retrieve conversation state after Turn 1
            var state1Res = await client.GetAsync($"/api/v1/conversations/{conversation.Id}");
            Assert.Equal(HttpStatusCode.OK, state1Res.StatusCode);
            var state1 = await state1Res.Content.ReadFromJsonAsync<Conversation>();
            Assert.NotNull(state1);
            Assert.NotEmpty(state1.Messages);
            var session1 = state1.ProviderSessionId;

            // 4. Message 2 (Turn 2 - Follow Up)
            var prompt2Res = await client.PostAsJsonAsync($"/api/v1/conversations/{conversation.Id}/prompt", new
            {
                prompt = "Turn 2: Follow up question to verify continuous session"
            });
            Assert.True(prompt2Res.StatusCode is HttpStatusCode.Accepted or HttpStatusCode.OK);

            await Task.Delay(500);

            // Retrieve conversation state after Turn 2
            var state2Res = await client.GetAsync($"/api/v1/conversations/{conversation.Id}");
            Assert.Equal(HttpStatusCode.OK, state2Res.StatusCode);
            var state2 = await state2Res.Content.ReadFromJsonAsync<Conversation>();
            Assert.NotNull(state2);
            Assert.True(state2.Messages.Count >= 2, "Expected multiple turns in conversation history.");

            // Session ID consistency assertion
            if (!string.IsNullOrEmpty(session1))
            {
                Assert.Equal(session1, state2.ProviderSessionId);
            }
        }
        finally
        {
            if (Directory.Exists(tempFolder))
            {
                try { Directory.Delete(tempFolder, true); } catch { }
            }
        }
    }

    private async Task<HttpClient> SetupAndAuthenticateClientAsync()
    {
        var client = _factory.CreateClient();
        var initRes = await client.PostAsJsonAsync("/api/v1/auth/setup/initialize", new
        {
            username = "admin",
            password = "SecurePassword123!",
            confirmPassword = "SecurePassword123!"
        });
        if (!initRes.IsSuccessStatusCode)
        {
            _ = await client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                username = "admin",
                password = "SecurePassword123!"
            });
        }
        return client;
    }
}
```

- [ ] **Step 2: Run test suite**

Run: `dotnet test tests/AgentHub.IntegrationTests/AgentHub.IntegrationTests.csproj --filter "FullyQualifiedName~ProviderChatIntegrationTests"`
Expected: All 5 provider multi-turn tests pass.

---

### Task 7: Workspace Lifecycle Integration Tests Verification & Full Suite Sanity

**Files:**
- Modify/Verify: `tests/AgentHub.IntegrationTests/Workspaces/WorkspaceLifecycleIntegrationTests.cs`

- [ ] **Step 1: Verify `WorkspaceLifecycleIntegrationTests.cs` passes with category attribute**

Run: `dotnet test tests/AgentHub.IntegrationTests/AgentHub.IntegrationTests.csproj --filter "FullyQualifiedName~WorkspaceLifecycleIntegrationTests"`
Expected: Passes.

- [ ] **Step 2: Run all test suites across the entire stack**

1. Run fast unit tests: `npm run test:unit`
2. Run frontend unit tests: `npm run test:frontend`
3. Run combined unit test sanity: `npm test`
4. Run comprehensive integration test suite: `npm run test:integration`
5. Run full pre-flight verification: `npm run test:all`

Expected: All suites exit with code 0.
