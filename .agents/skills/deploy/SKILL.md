---
name: deploy
description: "[Custom skill] Deploys and publishes the AI Agent Hub Web project using the publish profile, terminates locking processes, and starts/keeps the application running in the background."
author: Rafael Brasil
date_created: 2026-08-21
last_updated: 2026-08-25
---

# Deploy Web Project

Automates publishing and launching the AI Agent Hub Web project using the configured publish profile (`FolderProfile.pubxml`).

## Parameters & Triggers

- **Command Triggers**: `/deploy`, `deploy`, `publish app`, `deploy web project`, `deploy and run`
- **Supported Options**:
  - `-r`, `--run`: Starts the application immediately after publishing as a persistent background daemon (default port `5001`, default protocol `HTTPS`).
  - `-f`, `--foreground`: Starts the application attached in the foreground.
  - `-p <port>`, `--port <port>`: Binds the application to a specific port when running (default: `5001`).
  - `--protocol <http|https>`, `--http`, `--https`: Selects listening protocol (default: `https`).

## Critical Execution Rules

- **Default Protocol**: The application runs on **HTTPS** by default (`https://localhost:5001`, with HTTP fallback on `http://localhost:5002`), unless the user explicitly specifies a different protocol (e.g., `--http`).
- **Persistent Background Execution**: When `--run` is active or requested, the application starts as a detached background daemon.
- **NEVER Kill Deployed Application**: The AI assistant MUST NEVER terminate or kill the deployed `AIAgentHub.Web` process at the end of the turn. The general rule regarding process cleanup applies exclusively to temporary test instances spawned during automated test suites, NOT to user-requested deployments.

## Execution Workflow

1. **Parse Options**:
   - Check if the user explicitly provided `-r`, `--run`, `-p`, `--port <port>`, `--protocol <protocol>`, or `-f`.

2. **Check Active Instance (Pre-Deployment Confirmation)**:
   - If the user did **not** explicitly request to run the app (`--run` is not set):
     - Check if an active `AIAgentHub.Web` process is currently running (e.g. via `Get-Process AIAgentHub.Web` on Windows or `pgrep -f AIAgentHub.Web` on Unix).
     - If an active instance is running:
       - **Ask the user** if they want to re-run the application after publishing before killing the instance and starting the deploy.
       - If the user confirms, treat the invocation as having `--run` enabled.
       - If the user rejects or if no instance is running, proceed with publish-only.

3. **Execute Deployment**:
   - Run the deployment command based on the determined parameters:
     - **Publish only**:
       ```powershell
       npm run deploy
       ```
     - **Publish and run (default HTTPS port 5001)**:
       ```powershell
       npm run deploy:run
       ```
     - **Publish and run (custom port / options)**:
       ```powershell
       npm run deploy -- --run --port <port>
       ```

4. **Verify Process is Alive**:
   - Verify that the process is actively running:
     ```powershell
     Get-Process AIAgentHub.Web -ErrorAction SilentlyContinue
     ```
   - Report the HTTPS URL clearly to the user:
     - Default: `https://localhost:5001` (HTTP fallback: `http://localhost:5002`)
     - Custom: `https://localhost:<port>` (HTTP fallback: `http://localhost:<port+1>`)
