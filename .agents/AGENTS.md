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
