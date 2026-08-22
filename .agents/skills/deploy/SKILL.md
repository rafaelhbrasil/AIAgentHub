---
name: deploy
description: "[Custom skill] Deploys and publishes the AI Agent Hub Web project using the publish profile, terminates locking processes, and optionally starts the application."
author: Rafael Brasil
date_created: 2026-08-21
last_updated: 2026-08-21
---

# Deploy Web Project

Automates publishing and optionally running the AI Agent Hub Web project using the configured publish profile (`FolderProfile.pubxml`).

## Parameters & Triggers

- **Command Triggers**: `/deploy`, `deploy`, `publish app`, `deploy web project`
- **Supported Options**:
  - `-r`, `--run`: Starts the application immediately after publishing.
  - `-p <port>`, `--port <port>`: Binds the application to a specific port when running (default: `5001`).

## Execution Workflow

1. **Parse Options**:
   - Check if the user explicitly provided `-r`, `--run`, `-p`, or `--port <port>`.

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
     - **Publish and run (default port 5001)**:
       ```powershell
       npm run deploy:run
       ```
     - **Publish and run (custom port)**:
       ```powershell
       npm run deploy -- --run --port <port>
       ```

4. **Verify Output**:
   - Ensure the publish build completed successfully (exit code 0).
   - If running, confirm that the application started and report the local listening URL (e.g., `http://localhost:5001`).
