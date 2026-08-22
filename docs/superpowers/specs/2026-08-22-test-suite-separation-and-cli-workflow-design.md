# Test Suite Separation & Comprehensive Integration Test Matrix Design

## Overview
This design establishes the separation of fast Unit Tests from slower Integration/E2E Tests across both CLI (`npm` / `dotnet test`) and IDE (Visual Studio Test Explorer) environments in AgentHub, and specifies a **comprehensive integration test suite** covering the happy path of all core application features.

Prior to this design, running tests during standard development either ran all tests (including slow browser/server-dependent integration tests) or required skipping tests entirely. This design establishes dedicated npm scripts, assembly trait categorization, `.runsettings` filtering, and an end-to-end integration test suite covering authentication, recovery, provider detection, model configuration, workspace lifecycle, and multi-turn chat sessions across all AI providers.

---

## CLI & NPM Script Taxonomy

Root `package.json` will provide dedicated, standardized test commands:

```json
{
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

### Script Execution Summary

| Command | Targets | Expected Execution Time | Purpose |
| :--- | :--- | :--- | :--- |
| `npm test` | Frontend (Vitest) + Backend (`AgentHub.UnitTests`) | < 3 seconds | Fast sanity check before committing or pushing changes |
| `npm run test:frontend` | Frontend unit tests (`src/AIAgentHub.Web/frontend`) | < 2 seconds | Isolated React/DOM component verification |
| `npm run test:unit` | Backend unit tests (`tests/AgentHub.UnitTests`) | < 2 seconds | Isolated Domain, Application, and Infrastructure logic verification |
| `npm run test:integration` | Backend integration tests (`tests/AgentHub.IntegrationTests`) | ~5-15 seconds | `WebApplicationFactory` API & multi-provider multi-turn chat tests |
| `npm run test:all` | Frontend + Unit + Integration | Complete Suite | Full pre-release or CI verification |

---

## .NET & Visual Studio Test Explorer Architecture

### 1. Assembly Trait Categorization
- **`tests/AgentHub.UnitTests`**:
  ```csharp
  [assembly: AssemblyTrait("Category", "Unit")]
  ```
- **`tests/AgentHub.IntegrationTests`**:
  ```csharp
  [assembly: AssemblyTrait("Category", "Integration")]
  ```

### 2. Solution-Level `.runsettings`
A `test.runsettings` file at the repository root:
```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <RunConfiguration>
    <!-- Default filter for Visual Studio Test Explorer "Run All Tests" -->
    <TestCaseFilter>Category!=Integration</TestCaseFilter>
  </RunConfiguration>
</RunSettings>
```

### 3. Visual Studio Test Explorer Behavior
- **Default "Run All Tests"**: Executes all Unit Tests across the solution without triggering long-running browser tests or external integration fixtures.
- **Selective Execution**: In Test Explorer, developers can group by Project or Traits (`Unit` vs `Integration`) and right-click to run `AgentHub.IntegrationTests` at will.

---

## Comprehensive Happy Path Integration Test Matrix

All integration tests utilize ASP.NET Core `WebApplicationFactory<Program>` in `AgentHub.IntegrationTests` to execute the full application stack in-memory (real routing, auth cookies, EF Core SQLite database, controllers, and services).

### 1. Authentication & Security Flow (`AuthIntegrationTests.cs`)
- **Setup Status**: Anonymous user checks `/api/v1/auth/setup/status` (fresh system reports `isSetupCompleted: false`).
- **Initial Setup**: Admin account initialized with username and strong password via `/api/v1/auth/setup/initialize` (sets auth cookie).
- **Session Identity**: `/api/v1/auth/me` returns the authenticated admin user.
- **Login & Logout**: Login via `/api/v1/auth/login` sets cookie; `/api/v1/auth/logout` clears session.
- **Protected Endpoint Enforcement**: Unauthenticated requests to protected endpoints return `401 Unauthorized`.
- **Recovery & Reset**: System recovery code generation, password reset flow, and `/api/v1/auth/recover-wipe` safety checks.

### 2. Provider Management & Model Configuration (`ProvidersIntegrationTests.cs`)
- **Initial Discovery & DB Seeding**: First request to `/api/v1/providers` seeds discovery records to DB and returns cached providers.
- **Fast Cached Retrieval**: Repeated requests return cached detection records in sub-second time.
- **Parallel Refresh & SSE Stream**:
  - `/api/v1/providers/refresh` executes full parallel re-detection and returns updated status.
  - `/api/v1/providers/refresh/stream` streams Server-Sent Events (`init`, `progress`, `completed`).
- **Model Enable/Disable Settings**:
  - Retrieve models for a provider via `/api/v1/providers/{id}/models`.
  - Toggle model visibility via `POST /api/v1/providers/{id}/models/settings` (e.g. disable model `A`, enable model `B`).
  - Verify that subsequent requests return the updated model visibility state.

### 3. Workspace Lifecycle (`WorkspaceIntegrationTests.cs`)
- **Workspace Creation**: Create workspace pointing to a temporary test folder via `POST /api/v1/workspaces`.
- **Workspace Settings**: Update workspace settings (ignored files, default provider/model).
- **Listing & Details**: Fetch workspace list and single workspace details with stats.
- **Filesystem Listing**: Query `/api/v1/filesystem/list` within the workspace path.
- **Workspace Deletion**: Delete workspace and verify cascade cleanup.

### 4. Multi-Turn Chat & Session Continuity Across ALL Providers (`ProviderChatIntegrationTests.cs`)
Tests execute 2 consecutive messages per provider to verify full conversation orchestration and session ID preservation across turns:
- **Providers Tested**:
  1. `antigravity`
  2. `claudecode`
  3. `codexcli`
  4. `geminicli`
  5. `opencode`
- **Multi-Turn Flow (Turn 1 & Turn 2)**:
  1. Create a conversation in a workspace bound to the provider.
  2. **Turn 1**: Post prompt `"Turn 1: Hello from integration test"`.
     - Verify user message added to conversation history.
     - Verify provider execution starts session and produces assistant response.
     - Verify `ProviderSessionId` is captured and persisted on the conversation entity.
  3. **Turn 2**: Post follow-up prompt `"Turn 2: Follow up question"`.
     - Verify user message 2 added.
     - Verify assistant response 2 recorded.
     - Verify same `ProviderSessionId` is reused, maintaining conversational context and session continuity.
  4. **Workspace Snapshot & Diff Detection**: Verify snapshot service detects file modifications made during provider execution.

### 5. Application Settings & Diagnostics (`SettingsIntegrationTests.cs`)
- **Settings Management**: Get and update global application settings via `/api/v1/settings`.
- **Filesystem Drives**: Query `/api/v1/filesystem/drives` for valid drive roots.

---

## Obsolete Directory Cleanup
Remove leftover directories from prior reorganizations:
- `tests/AIAgentHub.Application.Tests`
- `tests/AIAgentHub.Domain.Tests`
- `tests/AIAgentHub.Infrastructure.Tests`
- `tests/AIAgentHub.Integration.Tests`
- `tests/AIAgentHub.Web.Tests`

---

## Documentation Updates
- Update root `README.md` to document the new `npm test` script taxonomy and testing instructions.
