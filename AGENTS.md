# Project Memory & Behavioral Instructions

## Specification-First Development & Documentation Synchronization

- Whenever an issue, request, or task is NOT a pure code bug, but instead represents a documentation change, specification gap, or new/modified feature:
  1. You MUST update the specification and requirements first (inside the `docs/` folder).
  2. Do NOT change documented behavior without updating the specification first.
  3. Do NOT add new behavior, features, or architecture components without adding them to the specification first.
  4. Only after the specification in `docs/` accurately documents the intended behavior and design should implementation changes be applied.
  5. When making changes to frontend code only, build only frontend via NPM and run only frontend unit/integration tests if any
  6. When making changes to backend code only, build only the backend via dotnet and run only backend unit/integration tests if any

## Test Execution & Process Watchdog Instructions
- When running integration tests, end-to-end browser tests, or tests/commands that interact with real external console/CLI executables (anything other than pure in-memory unit tests):
  - Always run a watchdog mechanism (such as a scheduled timer, monitoring task, or companion agent) in parallel with the execution.
  - The watchdog must actively check progress, verify logs are moving, prevent unmonitored blocking/freezes, and alert or terminate the process if it hangs or exceeds reasonable execution time limits.

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

