# Auto-Open Browser on Startup and Unconditional Console URL Logging Specification

## Overview
This specification defines the behavior, architecture, and configuration for automatically opening the user's system default browser upon application startup and unconditionally displaying the server's listening URLs in the terminal console.

When AI Agent Hub starts, it binds to network endpoints (defaulting to HTTPS on port 5432 and HTTP on port 5433, or custom ports passed via `--port` / `--urls`). To provide a smooth developer and user experience:
1. The server resolves the active bound listening addresses, converts wildcard hosts (`0.0.0.0`, `[::]`, `+`, `*`) into browser-accessible `localhost` URLs, and opens the primary URL in the system default browser.
2. The server outputs a clear, formatted startup banner directly to the console (`Console.Out`) so listening URLs are always visible, even when logging levels are configured to `Warning` or `Error`.
3. Auto-browser launch is configurable via command-line arguments and `appsettings.json`, and is automatically suppressed during automated tests and headless execution.

---

## Configuration & CLI Precedence

### 1. Precedence Hierarchy
The decision to launch the browser on startup follows this strict precedence:
1. **Automated Testing & Headless Safeguard**: If `IHostEnvironment.IsEnvironment("Testing")` or if the process is running in an automated test host, browser launching is **always disabled**.
2. **Command-Line Arguments**:
   - `--no-browser`, `-no-browser`, `/no-browser`: Explicitly disables launching the browser.
   - `--browser`, `-browser`, `/browser`: Explicitly enables launching the browser.
3. **Application Configuration (`appsettings.json` / Environment Variables)**:
   - `AgentHub:OpenBrowserAtStartup` (Boolean, default: `true`).
4. **Default Fallback**: If neither flag nor configuration is explicitly provided, the default is `true`.

### 2. Configuration Schema
In `src/AIAgentHub.Web/appsettings.json` and `appsettings.Development.json`:
```json
{
  "AgentHub": {
    "OpenBrowserAtStartup": true,
    "CliExecution": {
      "Headless": true,
      "Shell": "PowerShell",
      "HeadedAutoCloseDelaySeconds": 10
    },
    "PromptLogging": false
  }
}
```

---

## URL Resolution & Normalization

### 1. Bound Address Extraction
When `IHostApplicationLifetime.ApplicationStarted` triggers:
- Query `IServerAddressesFeature` via `IServer.Features.Get<IServerAddressesFeature>()?.Addresses`.
- If the feature is missing or empty, fall back to configured addresses from `builder.Configuration["urls"]` or `https://0.0.0.0:5432;http://0.0.0.0:5433`.

### 2. Wildcard Normalization
Listening URLs frequently bind to all network interfaces (`0.0.0.0` or `[::]`). For browser navigation and human display, hosts are normalized:
- Wildcards `0.0.0.0`, `[::]`, `+`, `*`, `0` ➔ `localhost`
- Loopback `127.0.0.1` ➔ `127.0.0.1` (or `localhost`)
- Explicit hostnames (e.g., `hub.local`) ➔ preserved as specified.

### 3. Primary URL Selection
If multiple addresses are bound:
- Prioritize `HTTPS` URLs over `HTTP` URLs.
- If only `HTTP` URLs are present, select the first `HTTP` URL.

---

## Console Logging & Browser Launch Execution

### 1. Unconditional Console Banner
To ensure users know where to access the application even when the logging configuration suppresses `Information` logs (`LogLevel.Default = "Warning"`):
- Write the startup banner directly to `Console.Out`:
  ```text
  ==============================================================
    AI Agent Hub is running!
    ➜ Local:    https://localhost:5432
    ➜ Fallback: http://localhost:5433
  ==============================================================
  ```
- If only a single URL is active (e.g., HTTP only on port 5001):
  ```text
  ==============================================================
    AI Agent Hub is running!
    ➜ Local:    http://localhost:5001
  ==============================================================
  ```

### 2. Cross-Platform Default Browser Launch
When `OpenBrowserAtStartup` resolves to `true` and not in `Testing`:
- **Windows**: `Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })`
- **macOS**: `Process.Start("open", url)`
- **Linux**: `Process.Start("xdg-open", url)`
- **Error Handling**: Wrapped in a try/catch block so headless environments, missing desktop sessions, or OS command errors log a warning without crashing or halting the server.

---

## Components to Update / Create

1. **`src/AIAgentHub.Web/Startup/StartupLifecycleService.cs`** (or `StartupUrlHelper.cs`):
   - Helper class to normalize listening addresses, select the primary browser URL, display the console banner, and trigger cross-platform browser launch.
2. **`src/AIAgentHub.Web/Program.cs`**:
   - Parse `--no-browser` / `--browser` CLI argument.
   - Register the lifecycle callback on `app.Lifetime.ApplicationStarted`.
3. **`src/AIAgentHub.Web/appsettings.json` & `appsettings.Development.json`**:
   - Add `"OpenBrowserAtStartup": true` under `"AgentHub"`.
4. **`README.md`**:
   - Document the auto-launch behavior, the `AgentHub:OpenBrowserAtStartup` config key, and the `--no-browser` CLI flag.
5. **`tests/AgentHub.UnitTests/Web/StartupUrlResolverTests.cs`**:
   - Unit tests validating address normalization, HTTPS preference, and configuration precedence.

---

## Verification Plan

### 1. Unit Tests
- Test wildcard replacement (`https://0.0.0.0:5432` ➔ `https://localhost:5432`, `http://[::]:8080` ➔ `http://localhost:8080`).
- Test primary URL selection (HTTPS priority over HTTP).
- Test CLI and configuration precedence resolution.

### 2. Automated Test Suite Integrity
- Execute `dotnet test` and `npm test` to verify that integration tests and unit tests run completely headless with 0 browser popups and 100% pass rate.
