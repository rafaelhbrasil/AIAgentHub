# Deploy Lifecycle & Background Process Management Design

## Overview
This specification defines the deployment and application runtime lifecycle for AI Agent Hub when publishing and launching the application via `npm run deploy`, `npm run deploy:run`, and the `/deploy` agent skill.

Prior to this specification, launching the application via `deploy.mjs --run` spawned the web process attached in the foreground. In agentic or scripted environments, exiting the deploy command or completing the agent turn caused the process to be terminated, leaving the user with a killed application. Additionally, default protocol expectations (HTTPS default on port 5001) required explicit documentation and enforcement.

---

## Deployment & Execution Requirements

### 1. Default Protocol & Port Binding
- **Default Protocol**: HTTPS is the default protocol.
- **Default Ports**:
  - Primary (HTTPS): `https://localhost:5001` (or `https://0.0.0.0:5001`)
  - Secondary/Fallback (HTTP): `http://localhost:5002` (or `http://0.0.0.0:5002`)
- **Custom Ports**: When a custom port `$P` is specified (e.g. `--port 8080`), HTTPS binds to `$P` and HTTP fallback binds to `$P + 1`.
- **Explicit Protocol/URLs**: If `--protocol http` or explicit `--urls` are specified, the server binds according to the specified protocol configuration.

### 2. Background Daemon by Default for `--run`
- When `deploy.mjs` is executed with `--run` (or `-r`):
  - The application is spawned as a **detached background process** (`detached: true`, `stdio: 'ignore'`, `unref()`).
  - `deploy.mjs` waits up to 2 seconds to verify that the process is alive and initialized.
  - `deploy.mjs` outputs the process PID and the listening URLs (defaulting to HTTPS):
    ```
    ✅ AI Agent Hub is running in the background (PID: <pid>)
    🔒 HTTPS: https://localhost:5001 (Default)
    🌐 HTTP:  http://localhost:5002
    ```
  - `deploy.mjs` exits with exit code 0.
- An optional `--foreground` (`-f`) flag allows attached foreground execution when interactive console streaming is explicitly requested.

### 3. Locking Process Termination
- Before publishing, `deploy.mjs` checks if any existing instance of `AIAgentHub.Web` is running and terminates it to release file locks on the publish directory.
- This ensures clean rebuilds without locked file errors.

### 4. Agent Skill Contract (`/deploy`)
- When the user asks to deploy and run the app or invokes `/deploy --run` / `/deploy -r`:
  1. The agent executes `npm run deploy:run` (or `npm run deploy -- --run --port <port>`).
  2. The application starts and persists in the background.
  3. The agent verifies the process is active (e.g. `Get-Process AIAgentHub.Web`).
  4. The agent **MUST NEVER** kill the deployed application upon completing the turn. (The global rule to terminate spawned processes applies exclusively to temporary test instances spawned during automated test suite runs).
  5. The agent reports the HTTPS listening URL (`https://localhost:<port>`).
