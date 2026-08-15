# Project Memory & Behavioral Instructions

## Specification-First Development & Documentation Synchronization

- Whenever an issue, request, or task is NOT a pure code bug, but instead represents a documentation change, specification gap, or new/modified feature:
  1. You MUST update the specification and requirements first (inside the `docs/` folder).
  2. Do NOT change documented behavior without updating the specification first.
  3. Do NOT add new behavior, features, or architecture components without adding them to the specification first.
  4. Only after the specification in `docs/` accurately documents the intended behavior and design should implementation changes be applied.
  5. When making changes to frontend code only, build only frontend via NPM and run only frontend unit/integration tests if any
  5. When making changes to backend code only, build only the backend via dotnet and run only backend unit/integration tests if any
