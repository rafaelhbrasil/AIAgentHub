# Project Memory & Behavioral Instructions

## Specification-First Development & Documentation Synchronization

- Whenever an issue, request, or task is NOT a pure code bug, but instead represents a documentation change, specification gap, or new/modified feature:
  1. You MUST update the specification and requirements first (inside the `docs/` folder).
  2. Do NOT change documented behavior without updating the specification first.
  3. Do NOT add new behavior, features, or architecture components without adding them to the specification first.
  4. Only after the specification in `docs/` accurately documents the intended behavior and design should implementation changes be applied.
  5. When making changes to frontend code only, build only frontend via NPM and run only frontend unit/integration tests if any
  6. When making changes to backend code only, build only the backend via dotnet and run only backend unit/integration tests if any

## Process Execution & Subagent Watchdog Instructions
- When executing child processes, package/tool installations, builds, integration tests, or commands that should complete within an expected duration:
  - The process MUST always be executed as a background subprocess (detached task or background process) to avoid having the interactive prompt window frozen.
  - Run a subagent/watchdog mechanism to actively monitor the execution progress, verify logs are advancing, and ensure the command is not blocked waiting for unhandled interactive prompts (e.g., sudo password, npm permissions, stdin).
  - If the process hangs, produces no progress, or exceeds reasonable timeouts, the watchdog must alert and terminate the process.
- When running a service or webserver that runs indefinitely (such as `dotnet run`, dev servers, or daemon processes):
  - Always run it as a detached background subprocess.
  - Run a subprocess or health check to verify that it is running (e.g., check listening ports or ping endpoint) so you know it is alive and the agent is not stuck, then continue or release the prompt.

## Database Migration Parity & Backward Compatibility
- Whenever modifying the database schema or creating/merging EF Core migrations:
  1. All new migrations must follow version prefix naming (e.g., `v0_2_0_...`).
  2. The `AgentHubDbContextModelSnapshot` and designer files must never have duplicate navigations or schema mismatches.
  3. Every migration change must be accompanied by running `DatabaseMigrationTests` to verify zero pending model changes (`HasPendingModelChanges() == false`) and clean step-by-step upgrade from previous versions.

## Git Commit Policy
- Never create git commits automatically after making changes unless:
  1. The user explicitly requests a commit (e.g., `/git-commit`, "commit this", etc.).
  2. The skill currently being executed contains an explicit commit step in its workflow.

## Documentation & Superpowers Artifacts
- The `docs/superpowers/` folder contains ephemeral session plans and brainstorming design specs.
- It is strictly ignored via `.gitignore` and must NEVER be staged, tracked, or committed to Git.

## Application Execution & Development vs. Deployment Policy
- **Running the Application**: When asked to run the app for testing or development, always run the default project using `dotnet run --project src/AIAgentHub.Web` or the default launchSettings profile on HTTPS port `5432` (`https://localhost:5432`).
- **Do NOT Deploy on Run/Rebuild/Test**: Never run deployment or publishing scripts (`npm run deploy`, `deploy:run`, etc.) when asked to run the application, rebuild code, or run unit tests.
- **Do NOT Kill Running Instances During Builds/Tests**: Never terminate or kill existing user application instances when rebuilding or running unit tests.
- **Deployment Script Scope**: `npm run deploy` and the `/deploy` skill are reserved exclusively for explicit user requests to publish or create a release bundle.

