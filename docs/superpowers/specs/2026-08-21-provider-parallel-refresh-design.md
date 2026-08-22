# Design: Fast Pre-Check, Parallel Loading & Real-time Progress Streaming for AI Providers

**Date:** 2026-08-21  
**Status:** Approved  
**Author:** Antigravity  

---

## 1. Overview

Enhance provider detection and refresh in AIAgentHub to deliver a responsive, observable, and resilient experience:
1. **Fast Pre-Check Detection:** Instant verification of installed providers via executable/binary discovery (`ExecutableName` and standard path searches) before running deeper checks.
2. **Parallel Provider Refresh:** Simultaneous execution of provider detailed status detection and model discovery for all installed providers. Uninstalled providers bypass detailed execution.
3. **Real-time SSE Progress Streaming:** A dedicated Server-Sent Events endpoint (`GET /api/v1/providers/refresh-stream`) streaming progress as each provider finishes.
4. **Interactive Progress Dialog:** A glassmorphic modal displaying a 0-100% animated progress bar and a live checklist of installed providers transitioning from spinners to their final status indicators (`✅ Operational`, `⚠️ Not Authenticated`, `❌ Failed`, `⏳ Quota Exceeded`, `⏹️ Discontinued`).
5. **Isolated Single-Provider Refresh:** Individual card refresh buttons remain lightweight with in-place spinners and without launching the full modal.

---

## 2. Backend Design

### 2.1 Fast Pre-Check (`IProvider` & `CliProviderBase`)

- **Interface:** Add `bool IsInstalledFastCheck()` to `IProvider`.
- **Implementation:** In `CliProviderBase`, `IsInstalledFastCheck()` invokes `!string.IsNullOrEmpty(FindExecutable(ExecutableName))` without executing child processes (`--version`, `auth status`, etc.).
- Providers with custom binaries (e.g. `AntigravityProvider` checking `agy` and fallback `antigravity`) override `IsInstalledFastCheck()` accordingly.

### 2.2 Streaming Refresh Endpoint (`GET /api/v1/providers/refresh-stream`)

- **Route:** `[HttpGet("refresh-stream")]` in `ProvidersController`.
- **Response Type:** `text/event-stream` with chunked transfer encoding and `no-cache`.
- **Execution Lifecycle:**
  1. `ProviderManager` performs the fast pre-check on all registered providers.
  2. Providers not installed (`IsInstalledFastCheck() == false`) are immediately recorded as `NotInstalled` and excluded from the parallel task batch.
  3. Emit SSE `init` event:
     ```json
     {
       "type": "init",
       "totalInstalled": 3,
       "providers": [
         { "id": "claude", "displayName": "Claude Code" },
         { "id": "antigravity", "displayName": "Antigravity CLI" },
         { "id": "opencode", "displayName": "OpenCode" }
       ]
     }
     ```
  4. Launch parallel tasks (`Task.WhenAll`) for each installed provider:
     - Execute `DetectDetailedAsync(cancellationToken)` and `GetModelsAsync(forceRefresh: true)`.
     - Persist detection record and models to SQLite database via scoped repositories.
  5. Upon completion of each individual provider task, immediately write an SSE `provider_completed` event:
     ```json
     {
       "type": "provider_completed",
       "provider": {
         "id": "claude",
         "displayName": "Claude Code",
         "status": "Ready",
         "message": "Provider is operational and ready to use.",
         "supportedModels": [...]
       },
       "completedCount": 1,
       "totalInstalled": 3,
       "percentage": 33
     }
     ```
  6. When all tasks conclude, emit SSE `completed` event containing full sorted `ProviderDto[]` list and close the stream.

### 2.3 Fault Isolation & Resilience

- Each parallel task runs inside an isolated `try/catch` block.
- If a provider CLI hangs or throws an exception, the provider is assigned `ProviderStatus.Error` with the failure message. The error does not disrupt the remaining concurrent provider tasks.

---

## 3. Frontend Design

### 3.1 Progress Dialog (`ProviderRefreshModal.tsx`)

- **Trigger:** Opened when clicking "🔄 Refresh All Providers" or on initial load when provider cache is missing.
- **States:**
  - **Connecting / Fast-checking:** Spinner with text *"Detectando providers instalados..."*.
  - **Active Refresh:**
    - Animated progress bar with smooth transition (`0%` to `100%`).
    - Numeric counter label (e.g. `2 / 3 finalizados • 66%`).
    - Live list of installed providers:
      - Pending: `[Spinner] Provider Display Name — Verificando...`
      - Completed: `Provider Display Name` with corresponding status tag:
        - `Ready` -> `✅ Operational`
        - `Unauthenticated` -> `⚠️ Not Authenticated`
        - `QuotaExceeded` -> `⏳ Quota Exceeded`
        - `Error` -> `❌ Failed`
        - `Discontinued` -> `⏹️ Discontinued`
  - **Finished (100%):**
    - The dialog remains open displaying the finalized summary list.
    - An enabled **"Concluído" / "Fechar"** button allows the user to dismiss the dialog at their convenience.
    - Main `ProvidersView` provider state is refreshed in the background.

### 3.2 Single-Provider Refresh (`ProviderCard.tsx`)

- Clicking "🔄 Refresh" on an individual `ProviderCard` performs a localized refresh (`/api/v1/providers/{id}/status?refresh=true` and `/models?refresh=true`).
- Renders an in-place spinner on the card button/badge (`Checking...`).
- Does not launch the global modal.
- Displays a toast upon completion.

---

## 4. Verification & Testing Plan

1. **Unit Tests (Backend):**
   - `ProviderManagerTests`: Verify `IsInstalledFastCheck` accurately filters uninstalled providers and parallel execution updates all records.
   - `ProvidersControllerTests`: Verify SSE streaming format, headers, and event sequence (`init` -> `provider_completed` -> `completed`).
2. **Integration / End-to-End Tests (Frontend):**
   - Verify modal opens on "Refresh All Providers", progress bar advances smoothly, and completed items display the correct icons/badges.
   - Verify uninstalled providers are omitted from the progress modal checklist.
   - Verify single-provider refresh remains localized on `ProviderCard`.
