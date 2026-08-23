# Implementation Plan: Live Provider Chat Integration Tests & Session Consistency

This plan details the implementation of real, unmocked live provider chat integration tests verifying session continuity and memory retention across all installed AI CLI providers (`antigravity`, `claude`, `codex`, `gemini`, `opencode`).

---

## User Review Required

> [!NOTE]
> Tests will automatically detect which AI CLIs are installed on your machine.
> - If an AI provider (e.g. `claude`, `opencode`) is **not installed** or **not authenticated**, the test will be dynamically **Skipped** (`Assert.Skip`), reporting as "Skipped / Ignored" in test output.
> - If an AI provider is **installed and authenticated**, it will execute real prompts to verify 2-turn memory recall with a 60s per-turn watchdog timeout.

---

## Proposed Changes

### Integration Tests Layer (`tests/AgentHub.IntegrationTests`)

#### [NEW] `LiveProviderWebApplicationFactory.cs`
- In `tests/AgentHub.IntegrationTests/Web/Chat/LiveProviderWebApplicationFactory.cs`:
  - Inherits from `WebApplicationFactory<Program>`.
  - Configures isolated temporary SQLite database in `%TEMP%` (`AgentHubLiveTest_<guid>.db`).
  - Sets `NetworkMode = NetworkMode.Localhost`.
  - **Retains real `HeadlessProcessExecutor`** (does not replace `IProcessExecutor` with a mock).
  - Cleans up and deletes temporary `.db` file on disposal.

#### [NEW] `LiveProviderChatIntegrationTests.cs`
- In `tests/AgentHub.IntegrationTests/Web/Chat/LiveProviderChatIntegrationTests.cs`:
  - Parameterized `[Theory]` across all 5 providers: `"antigravity"`, `"claude"`, `"codex"`, `"gemini"`, `"opencode"`.
  - Step 1: Query `IProviderManager.GetProvider(providerId)`.
  - Step 2: If provider is null or not installed (`!await provider.IsInstalledAsync()`), call `Assert.Skip($"Provider '{providerId}' is not installed.")`.
  - Step 3: If provider is unauthenticated or discontinued, call `Assert.Skip(...)`.
  - Step 4: Generate a random 6-digit number (`var secret = Random.Shared.Next(100000, 999999);`).
  - Step 5: Turn 1: Post prompt `$"Remember the number {secret}. Reply with ACKNOWLEDGED."` -> wait for completion via watchdog -> verify `ProviderSessionId`.
  - Step 6: Turn 2: Post prompt `"What was the number I asked you to remember in this session? Reply with only the number."` -> wait for completion -> assert `ProviderSessionId` matches Turn 1 -> assert Assistant message contains `{secret}`.

---

## Verification Plan

### Automated Verification
1. Run only live provider tests:
   ```bash
   dotnet test tests/AgentHub.IntegrationTests/AgentHub.IntegrationTests.csproj --filter "FullyQualifiedName~LiveProviderChatIntegrationTests"
   ```
2. Run complete integration suite:
   ```bash
   npm run test:integration
   ```
3. Run full verification:
   ```bash
   npm run test:all
   ```
