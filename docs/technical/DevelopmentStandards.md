# AI Agent Hub

# Development Standards

**Version:** 0.1 Draft

---

# Purpose

This document defines the engineering standards used throughout AI Agent Hub.

Its purpose is to ensure consistency, maintainability, quality and long-term sustainability of the project.

Every contributor is expected to follow these standards.

---

# General Principles

The project values:

- Simplicity
- Readability
- Maintainability
- Extensibility
- Testability
- Security

Readable code is preferred over clever code.

Explicit code is preferred over implicit behavior.

Maintainability is preferred over premature optimization.

---

# Technology Stack

Current technology choices:

Backend
- .NET 10
- ASP.NET Core
- C#
- Entity Framework Core
- SQLite
- SignalR (real-time: streaming, events, progress) — see ADR-010

Frontend
- React
- TypeScript
- Vite

Technology decisions are documented separately through ADRs.

---

# Project Architecture

The project follows:

- Clean Architecture
- Domain Driven Design (DDD)
- API First
- Server-Centric Architecture

See:

- Architecture.md

---

# Coding Style

## Language

Use modern C# features whenever they improve readability.

Avoid obsolete APIs.

---

## Naming

Use descriptive names.

Avoid abbreviations unless universally recognized.

Examples:

Good:

- WorkspaceService
- ProviderManager
- ConversationRepository

Bad:

- WSMgr
- Util
- Misc

Always use the terminology defined in Glossary.md.

---

## Classes

A class should have one clear responsibility.

Large classes should be decomposed.

---

## Methods

Methods should:

- perform one task
- remain short
- avoid excessive nesting

Guard clauses are preferred over deeply nested conditions.

---

## Comments

Code should explain **why**, not **what**.

Avoid redundant comments.

Bad:

```csharp
// Increment i
i++;
```

Good:

```csharp
// Retry because the provider may still be starting.
```

---

# Nullable Reference Types

Nullable reference types must remain enabled.

Warnings should not be ignored.

---

# Warnings

Compiler warnings should be treated as errors.

The project should compile warning-free.

---

# Dependency Injection

Constructor injection is the preferred approach.

Avoid:

- service locators
- static service access

---

# Asynchronous Programming

Use async/await whenever appropriate.

Avoid synchronous blocking on asynchronous operations.

Examples to avoid:

- Task.Wait()
- Result

---

# Exceptions

Exceptions represent exceptional situations.

Do not use exceptions for ordinary control flow.

---

# Logging

Log meaningful events.

Avoid excessive logging.

Sensitive information must never be logged.

---

# Configuration

Configuration should be strongly typed.

Magic strings should be avoided.

---

# Persistence

Business logic must not depend on Entity Framework.

Repositories expose abstractions.

Infrastructure implements persistence.

---

# Testing

Every new feature should include tests.

Testing pyramid:

- Unit Tests
- Integration Tests

Avoid UI tests unless necessary.

---

# Unit Tests

Unit tests should:

- be deterministic
- run quickly
- avoid external dependencies

---

# Integration Tests

Integration tests validate interactions between components.

They should execute against isolated environments.

---

# Code Reviews

Every Pull Request should verify:

- correctness
- readability
- architecture
- security
- tests

---

# Documentation

Every significant feature should update:

- Product documentation
- Release documentation
- ADRs (if architecture changes)

Documentation is part of the implementation.

---

# Git

Recommended branch naming:

feature/

bugfix/

hotfix/

release/

---

# Commit Messages

Commit messages should be clear and descriptive, explaining what was changed and why.

Guidelines:

- State the purpose of the change clearly in the first line.
- Provide additional context or reasoning in the description when helpful.
- Avoid vague messages like `update`, `fix`, `wip`, or `changes`.

Examples:

```text
Add provider capability detection for Antigravity CLI
Resolve workspace synchronization bug when switching projects
Update product documentation for version 0.1 release
```

---

# Pull Requests

A Pull Request should:

- compile
- pass all tests
- update documentation
- contain focused changes

Avoid mixing unrelated work.

---

# Performance

Optimize only after measurement.

Correctness comes before optimization.

Maintainability comes before micro-optimizations.

---

# Security

Security is everyone's responsibility.

Every change should consider:

- authentication
- authorization
- secret management
- logging
- input validation

---

# Backward Compatibility & Database Migrations

Breaking changes should be avoided.

When unavoidable:

- document them
- justify them
- include migration guidance

## Database Migration Lifecycle & Parity Standards

To prevent schema desynchronization, designer file corruptions, or upgrade failures across releases:

1. **Model Parity Enforcement (`HasPendingModelChanges`):**
   - The compiled EF Core `DbContext` model and `AgentHubDbContextModelSnapshot` must always be in 100% parity.
   - An automated unit test verifies `context.Database.HasPendingModelChanges() == false` on every test run.
2. **Step-by-Step Upgrade Verification:**
   - Incremental version migrations (e.g. `v0.1.0` $\rightarrow$ `v0.2.0`) must be verified via automated migration tests using `IMigrator` to ensure existing databases upgrade cleanly without `NOT NULL` constraint violations or data loss.
3. **Migration Naming & Squashing Protocol:**
   - All migrations belonging to a minor release must include the version prefix in their class and migration identifier (e.g., `v0_2_0_AddVersion02MultiProviderTracking`).
   - When squashing migrations for a baseline release, use official EF Core tooling (`dotnet ef migrations add`) to generate pristine `Designer.cs` and `ModelSnapshot.cs` files rather than manual snippet editing.
   - Never define duplicate `OwnsOne` or scalar mappings for the same property.

---

# Multi-Theme Architecture & Color Contrast Standards

## Theme Variable Separation & Accessibility
The frontend supports **Dark**, **Light**, and **System** themes via CSS variables defined on `:root` and overridden under `html.light`.
To ensure WCAG AA color contrast and theme fidelity:

1. **CSS Variable Exclusivity**:
   - Never use hardcoded dark colors (e.g. `#000`, `#080c14`, `rgba(0, 0, 0, 0.25)`) or light colors (e.g. `#fff`, `#f8fafc`) directly in component inline styles or non-scoped CSS rules for surfaces and text.
   - Surfaces must use semantic CSS variables: `var(--bg-primary)`, `var(--bg-secondary)`, `var(--bg-card)`, `var(--bg-glass)`, `var(--bg-subtle)`, and `var(--bg-input)`.
   - Text must use semantic CSS variables: `var(--text-main)`, `var(--text-muted)`, `var(--text-heading)`.
2. **Stat & Heading Contrast**:
   - Number and stat displays (`.stat-val`) must utilize `--stat-gradient` with high-contrast foreground gradients in both dark mode (`linear-gradient(135deg, #ffffff, #94a3b8)`) and light mode (`linear-gradient(135deg, #0f172a, #475569)`).
3. **Form Controls & Options**:
   - Form inputs (`.form-input`, `.form-select`, `.form-textarea`, `.compact-select`) must adapt `background` and `color-scheme` to `light` when `html.light` is active, ensuring `<select>` dropdown options have clean white backgrounds with dark legible text.
4. **Chat Bubbles & Code Blocks**:
   - Assistant and user message bubbles must use theme variables (`--bg-card`, `--border-color`, `--text-main`) to prevent dark container styling from conflicting with dark font colors in light mode.
   - Code blocks and diff views must provide clear foreground-to-background contrast across both themes without breaking dark mode styling.

---

# File Diff Viewer & Mobile Navigation Architecture

## Smart Path & Unified Header Navigation
The file diff review dialog (`DiffViewerModal`) provides a responsive, single-tier navigation header for multi-file reviews:

1. **Smart Path Splitting**:
   - File paths are divided into **Directory Path** (`.../parent/`) and **Filename** (`FileName.ext`).
   - The directory path is rendered in a subtle, muted color with leading truncation if space is constrained.
   - The filename is rendered in bold high-contrast text (`var(--text-heading)`), ensuring mobile viewports (375px+) always prioritize filename visibility over root workspace prefixes.
2. **Unified Navigation & Dropdown Switcher**:
   - Sequential navigation (`[ ◀ ] [ ▶ ]` buttons and `Alt+Left` / `Alt+Right` keyboard shortcuts) are embedded directly in the header when multiple files exist.
   - The path title acts as an interactive dropdown trigger with a subtle chevron (`▾`), opening a quick file selector listing all changed files with their status badges (`Modified`, `Created`, `Deleted`) and addition/deletion counts.
   - Redundant secondary file tab rows are removed to maximize vertical screen space for code diff viewing.
3. **Pinned Status & Metadata**:
   - The change status badge (`Modified`, `Created`, `Deleted`) and line diff metrics (`+N -N`) are pinned with `flex-shrink: 0`, preventing wrapping or clipping on narrow viewports.

---

# Conversation Execution Concurrency & Reconnection Architecture

To prevent race conditions, duplicate prompt executions, and UI state loss across page reloads:

1. **Single-Execution Invariant per Conversation**:
   - Each conversation enforces at most one active execution (prompt processing or provider streaming) at any given time.
   - Active executions are tracked in memory via a singleton `IActiveExecutionTracker` and `IProcessExecutor`.
   - Any concurrent request to `POST /api/v1/conversations/{id}/prompt` for a running conversation is rejected with HTTP `409 Conflict` (`execution_in_progress`).

2. **Execution State Propagation & Reconnection**:
   - Conversation detail queries (`GET /api/v1/conversations/{id}`) expose the `isRunning` boolean flag.
   - When loading or reloading a conversation in the frontend, if `isRunning` is `true`, the chat interface automatically switches to the streaming state (`isStreaming = true`), displaying the **Abort** button (`⏹`) and heartbeat indicator instead of the **Send** button.
   - SignalR events (`conversation.started`, `conversation.heartbeat`, `conversation.completed`, `conversation.aborted`) dynamically sync execution state in real-time across multiple tabs or following page reconnects.

---

# Versioning & Release Management

## Version Strategy
The project follows Semantic Versioning (`MAJOR.MINOR.PATCH`).

- Solution-wide versioning is centralized in root `Directory.Build.props` via `<BaseVersion>`.
- In **Debug** configuration, MSBuild automatically appends dynamic build timestamp metadata (`$(BaseVersion)-debug.MMddHHmm`) for build diagnostics and intraday build tracking.
- In **Release** configuration, strict semantic versions (`$(BaseVersion)`) are generated without build suffixes. When running deployed/release binaries, the 4th zero-revision segment is omitted in the header badge (e.g. `v0.2.0` instead of `v0.2.0.0`).
- The Web frontend embeds `__APP_VERSION__` at build time from `package.json` for zero-latency initial rendering, and asynchronously queries `GET /api/v1/system/version` to display detailed build numbers in Debug mode while displaying clean 3-part semantic versions in Release mode.

## Release Command
Releases are prepared and packaged using the automated release workflow by specifying a target publish profile (`win64` or `portable`):

```bash
npm run release <profile> [version] [options]

# Examples:
npm run release win64 0.1.0
npm run release portable 0.1.0
npm run release win64 0.1.0 -- --create-tag
```

The script:
1. Resolves the publish profile from `src/AIAgentHub.Web/Properties/PublishProfiles/<profile>.pubxml` and dynamically reads the target destination path from its `<PublishUrl>` tag.
2. Validates semantic version format (or auto-detects version from Git tag or `package.json` if omitted).
3. Synchronizes version across `package.json`, `frontend/package.json`, `Directory.Build.props`, and `Changelog.md`.
4. Runs full test suites (`dotnet test` and `npm test`).
5. Publishes release binaries via `dotnet publish -c Release /p:PublishProfile=<profile>`.
6. Creates a versioned distribution archive (`archive/AIAgentHub-v<version>_<profile>.zip`) with an accompanying SHA-256 integrity checksum (`archive/SHA256.txt`).

---

# Continuous Improvement

These standards evolve with the project.

When improvements are identified, prefer updating this document rather than relying on tribal knowledge.