# AI Agent Hub

# Architecture

**Version:** 0.1 Draft

---

# Purpose

This document describes the high-level architecture of AI Agent Hub.

It explains how the major components interact and establishes the architectural principles that guide future development.

Implementation details such as frameworks, libraries and coding standards are documented separately in **DevelopmentStandards.md**.

---

# Architectural Goals

The architecture is designed to achieve the following goals:

- Simplicity
- Maintainability
- Extensibility
- Testability
- Security
- Provider Independence

Every architectural decision should reinforce these goals.

---

# High-Level Architecture

AI Agent Hub follows a Server-Centric architecture.

The Server hosts all business logic.

Remote Stations act as lightweight clients.

```
             +-----------------------+
             |   Remote Station      |
             |     (Browser)         |
             +-----------+-----------+
                         |
                  HTTPS / WebSocket (SignalR)
                         |
+------------------------------------------------------+
|                    AI Agent Hub                      |
|------------------------------------------------------|
|                    Web UI                            |
|------------------------------------------------------|
|                 REST / WebSocket (SignalR) API         |
|------------------------------------------------------|
|                 Application Layer                    |
|------------------------------------------------------|
|                    Domain Layer                      |
|------------------------------------------------------|
| Infrastructure | Providers | Storage | Background    |
+------------------------------------------------------+
```

---

# Core Components

The application is composed of six primary layers.

## Presentation

Responsible for:

- Web UI
- REST API
- WebSocket API (SignalR)

Contains no business logic.

---

## Application

Coordinates use cases.

Responsible for:

- commands
- queries
- workflows
- orchestration

Application services should remain thin.

---

## Domain

Contains business rules.

The Domain Layer should not depend on any external framework.

Examples include:

- Workspace
- Conversation
- Provider
- Permission

---

## Infrastructure

Responsible for:

- persistence
- file system
- networking
- encryption
- Git
- provider and CLI process execution (`IProcessExecutor`, `HeadlessProcessExecutor`, `HeadedProcessExecutor`)

Infrastructure depends on the Domain.

Never the opposite.

### Process Execution Architecture

All CLI execution (live prompt streaming via `ExecuteAsync` and auxiliary CLI command executions such as `--version`, model listings, and auth status checks via `RunCommandAsync`) is handled exclusively through `IProcessExecutor`. The dependency injection container resolves the appropriate executor (`HeadlessProcessExecutor` or `HeadedProcessExecutor`) based on configuration (`AgentHub:CliExecution:Headless`), ensuring provider adapters remain decoupled from process execution modes.

#### Execution Timeout & Keepalives
- **Configurable Timeout**: `AgentHub:CliExecution:TimeoutMinutes` (default: `10` minutes) sets the maximum execution duration for an individual prompt turn. Standard timeout environment variables (`API_TIMEOUT_MS`, `REQUEST_TIMEOUT_MS`, `TIMEOUT`, `CLI_TIMEOUT`, `ANTIGRAVITY_TIMEOUT_MS`, `ANTHROPIC_TIMEOUT_MS`, `OPENAI_TIMEOUT_MS`) are injected into child processes. Process execution timeout is managed directly by `IProcessExecutor` via cancellation tokens; provider adapters omit blocking CLI-specific wait flags (such as Antigravity CLI's `--print-timeout`) to allow background daemon tasks (e.g., dev web servers) to continue running after prompt turn completion without freezing the CLI.
- **Headed Process Decoupling**: In headed execution (`HeadedProcessExecutor`), CLI commands are launched with process-level redirection (`cmd.exe /c ... > log 2>&1` on Windows, or background detachment with `tail --pid` on Linux) while the terminal tails the log in real time. The runner monitors the primary CLI process handle (`WaitForExit()` or `tail --pid="$CLI_PID"`) rather than blocking on an anonymous OS pipeline (`| ForEach-Object`), ensuring that background child processes inheriting output handles do not deadlock the desktop terminal window upon CLI completion.
- **Cross-Platform Linux & WSL Execution**: Process executors run natively on Windows, Linux, and macOS. `HeadlessProcessExecutor` uses standard .NET asynchronous stream redirection across all platforms. `HeadedProcessExecutor` detects the operating system dynamically: on Windows it generates a PowerShell runner (`.ps1`); on Linux and WSL it generates a POSIX bash script (`.sh`) using `tail --pid` handle decoupling, launching it via `wt.exe` (on WSL) or Linux desktop terminal emulators (`x-terminal-emulator`, `gnome-terminal`, `konsole`, `xfce4-terminal`, `xterm`). In WSL environments (`WSL_DISTRO_NAME`), executable discovery searches both native Linux binaries and Windows-mounted CLI executables (`.exe`, `.cmd`, `.bat`).
- **Keepalive Heartbeat**: Periodic `conversation.heartbeat` SignalR events (every `HeartbeatIntervalSeconds`, default: 60s) provide live elapsed time and friendly progress feedback (`"Still working..."`, `"Analyzing codebase..."`) to the user interface during long thinking phases.
- **Bounded Auto-Resumption**: If a provider turn times out and `AutoResumeOnTimeout` is enabled, `ExecutionOrchestrator` automatically triggers a continuation turn (`"Continue from where you left off."`) in the same session, bounded by `MaxAutoResumes` (default: 2) to prevent infinite loops.
- **Child Process Environment Isolation**: Because AgentHub runs as an ASP.NET Core server, environment variables like `ASPNETCORE_URLS` and `DOTNET_LAUNCH_PROFILE` can leak into child terminal sessions and CLI subprocesses. Process executors explicitly strip these variables from child process start info and runner scripts to prevent guest workspace web applications from colliding with AgentHub's host ports (5432/5433).
- **Detached Background Execution Intent Gating**: When AI coding agents launch long-running background tasks (e.g. web servers, APIs, daemons), certain agent CLIs (notably GitHub Copilot CLI, OpenCode, and OpenAI Codex CLI) execute shell tool commands using internal synchronous process waits, custom multi-second sleep/curl loops, or risk context compaction forgetting. If a persistent process runs in the foreground or loops in verification, execution can deadlock or exceed runner timeouts. To prevent this without wasting input tokens on every turn, AgentHub implements intent/keyword gating:
  - Configurable trigger words are defined globally in `AgentHub:CliExecution:DetachedExecutionTriggerWords` (default: `run`, `start`, `launch`, `serve`, `server`, `host`, `daemon`, `listen`, `up`, `project`, `calc`, `web`, `execute`) and can be extended or overridden per provider in `Providers:<provider>:DetachedExecutionTriggerWords`.
  - When the incoming prompt matches any configured trigger word at word boundaries, the provider adapter prepends a targeted instruction instructing the CLI agent to launch background services detached (`Start-Process -FilePath 'dotnet' -ArgumentList 'run' -WindowStyle Hidden`) and return immediately without running sleep, wait loops, or post-startup curl/ping checks so the turn completes promptly.
  - Prompts that do not match execution keywords (e.g. code reviews, refactorings, explanations) incur zero token overhead.

---

## Provider Layer

Each AI provider is implemented as an adapter inheriting from `CliProviderBase`. Providers delegate all process execution to the injected `IProcessExecutor`.

When building CLI arguments, if a conversation is set to "Default Model" (or if `ModelId` is empty, `"default"`, `"auto"`, etc.), provider adapters omit the `--model` CLI argument completely to let the underlying CLI resolve to its own configured or upstream default model.

Provider-specific CLI argument formatting:
- **Antigravity CLI**: `--output-format text --add-dir "<workspace>" --mode accept-edits -p "<prompt>"` with optional `--model`, `--effort`, and `--conversation` (omitting `--print-timeout` so `agy` defaults to `0s` and exits immediately upon turn completion without blocking on background tasks).
- **Claude Code**: `--output-format text --permission-mode acceptEdits -p "<prompt>"` with optional `--model`, `--effort`, `--session-id` (initial message) and `--resume` (subsequent messages).
- **OpenAI Codex CLI**: `exec --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check "<prompt>"` (or `exec resume ... <session-id> "<prompt>"` on subsequent turns), with optional `--model` and `-c model_reasoning_effort=<effort>`. Automatically gates and injects detached background process execution instructions when prompt intent matches configured `DetachedExecutionTriggerWords` to avoid verification wait loops and runner timeouts.
- **OpenCode**: `run --dir "<workspace>" --agent agenthub --auto "<prompt>"` with optional `--model`, `--variant`, `--session`. Automatically provisions the `agenthub` agent configuration and injects detached background process execution instructions when prompt intent matches configured `DetachedExecutionTriggerWords` to avoid tool execution aborts or context-compaction amnesia.
- **GitHub Copilot**: `--output-format text --silent --allow-all-tools --add-dir "<workspace>"` with optional `--model`, `--session-id` (initial message) and `--resume` (subsequent messages). Automatically gates and injects detached background process execution instructions when prompt intent matches configured `DetachedExecutionTriggerWords` to avoid synchronous shell wait deadlocks.

---

## Chat Studio & Diff Viewer Interaction Patterns

### Chat Input Bar State Machine
- When a prompt turn is actively executing (streaming), the Send button (`#sendPromptBtn`) is hidden and replaced by the Abort button (`#abortBtn`).
- The user can type their next prompt in the chat input area while the current prompt executes, but cannot submit until the ongoing execution finishes or is aborted.
- Upon turn completion or abort, the Abort button is hidden and the Send button becomes visible.
- Provider switching is locked/disabled while a command is actively running until it completes or is aborted.

### Diff Reviewer Direct Hunk Focus
- In the file diff viewer, the change hunk indicator immediately displays `1 / <Total>` (e.g., `1 / 10`) upon opening or switching files instead of an unselected `<Total> diffs` label.
- The diff viewer automatically and immediately focuses/scrolls to the first change hunk without requiring a manual user click.

Examples:

- CodexProvider
- GeminiProvider
- ClaudeProvider
- OpenCodeProvider
- AntigravityProvider
- GitHubCopilotProvider

The remainder of the application communicates only through abstractions.

---

# Background Services

Responsible for long-running operations.

Examples:

- provider monitoring
- synchronization
- update checks
- cleanup

---

# Request Flow

A typical request follows this path.

```
Browser

↓

REST API / WebSocket (SignalR)

↓

Application Service

↓

Domain

↓

Provider Adapter

↓

AI CLI (stdin/stdout)

↓

SignalR

↓

Browser
```

No UI component communicates directly with providers.

---

# API First

Every feature exposed by the UI must be implemented through the public API.

The local Web UI consumes the same endpoints as Remote Stations.

This eliminates duplicate execution paths.

---

# Server-Centric Design

The Server owns:

- Workspaces
- Conversations
- Providers
- MCPs
- Skills
- Authentication
- Configuration

Remote Stations never execute AI providers directly.

---

# Provider Abstraction

Every provider implements a common interface.

```
IProvider

├── AntigravityProvider (agy)
├── CodexProvider
├── GeminiProvider
├── ClaudeProvider
├── OpenCodeProvider
└── GitHubCopilotProvider
```

The remainder of the application remains provider-independent.

---

# Workspace Architecture

A Workspace represents a logical project.

Future versions may support different origins.

Examples:

- Server
- Remote Station
- Git Repository
- ZIP Archive

The application should treat every Workspace identically after creation.

---

# Content Rendering Architecture

AI Agent Hub uses a pluggable content rendering architecture to preview files and visualize differences generated during AI interactions.

The Web UI is responsible only for presenting rendered content.

Preview selection and rendering logic always execute on the Server.

---

## Objectives

The rendering architecture is designed to:

- Support multiple file formats.
- Provide a consistent preview experience.
- Allow new renderers to be added without modifying existing components.
- Support future plugin-based renderers.
- Share infrastructure between Preview and Diff rendering.

---

## Architecture

```text
File

↓

Content Rendering Manager

↓

Renderer Registry

↓

Content Renderer

↓

HTML Response

↓

Browser
```

The browser never performs format-specific rendering decisions.

---

## Renderer Registry

Renderers register themselves with the Content Rendering Manager.

Each renderer declares:

- Supported file extensions
- Supported MIME types
- Rendering priority

The manager selects the most appropriate renderer for the requested content.

---

## Built-in Preview Renderers

Version 0.1 includes renderers for:

### Text

Examples:

- .txt
- .log
- .cs
- .ts
- .js
- .css
- .html
- .sql

Features:

- Plain text
- Syntax highlighting (when applicable)

---

### Markdown

Examples:

- .md
- .markdown

Features:

- HTML rendering
- GitHub-flavored Markdown support (planned)

---

### Images

Examples:

- .png
- .jpg
- .jpeg
- .gif
- .webp
- .bmp
- .svg

Features:

- Native image preview
- Zoom (future)

---

### JSON

Features:

- Pretty formatting
- Syntax highlighting
- Collapsible nodes (future)

---

### XML

Features:

- Pretty formatting
- Syntax highlighting
- Collapsible nodes (future)

---

### YAML

Examples:

- .yaml
- .yml

Features:

- Pretty formatting
- Syntax highlighting

---

### PDF & HTML (Version 0.3)

Examples:

- .pdf
- .html
- .htm

Features:

- Native sandboxed preview
- PDF embed / base64 object viewer

---

### Structured Data (Version 0.3)

Examples:

- .csv
- .tsv

Features:

- Responsive tabular grid presentation
- Column headers and cell formatting

---

### Config & Logs (Version 0.3)

Examples:

- .log
- .ini
- .toml
- .env

Features:

- Key-value highlighting
- Log stream timestamp formatting

---

## Diff Rendering

The rendering architecture is also used for file differences.

Version 0.1 supports:

- Unified diff
- Side-by-side diff

Diff rendering is independent from preview rendering but shares the same rendering infrastructure.

---

# Git Integration Architecture (Version 0.3)

AI Agent Hub integrates native Git repository operations into the workspace environment without requiring third-party library dependencies (such as libgit2 binaries):

- **Service Abstraction**: `IGitService` in the Application layer defines repository status, branch management, commit, push, pull, fetch, stash, history, commit details, commit diff, and clone operations.
- **Process-Based Execution**: `GitCliService` in the Infrastructure layer uses the host `git` command-line executable via `IProcessExecutor.RunCommandAsync`.
- **Workspace Isolation**: Git commands execute strictly within `workspace.Path`.
- **Optional Git Host**: If `git` is absent from the host machine, the service returns `isGitInstalled: false`, and the UI indicates that Git is optional without throwing exceptions.
- **Repository Cloning**: `POST /api/v1/workspaces/clone` clones a remote Git repository into a target path and automatically registers a new workspace with `Origin = WorkspaceOrigin.Git`.
- **Git Workbench & TortoiseGit Log Model**:
  - **Desktop Studio Mode Toggle**: Studio Header provides a top-level segmented switch between `[ 💬 Chat Studio ]` and `[ 🌿 Git Workbench ]`, dedicating the full 70% central desktop viewport to version control without drawer clutter.
  - **Log & Graph Explorer (Top Pane)**:
    - **Row 0 (Live Working Tree)**: Interactive head row representing uncommitted changes (staged, modified, untracked). Selecting it directs the bottom pane to the staging and commit form.
    - **Rows 1..N (Commit History)**: Chronological commit table with branch/tag pills, short hash, commit message, author, and timestamp.
    - **Pure SVG Lane Allocation Algorithm**: A lightweight, zero-dependency lane calculation algorithm renders branch lanes, commit nodes (`●`), and merge/fork bezier curves (`<path>`) aligning with each table row, providing high-fidelity visual history without heavy charting dependencies.
  - **Context-Sensitive Inspector (Bottom Pane)**:
    - *When Working Tree is selected*: Staged / Unstaged lists, file staging/unstaging controls, commit message input, and Commit/Stash buttons.
    - *When a Historical Commit is selected*: Commit metadata card, full multi-line commit message, and changed files list with additions/deletions.
  - **Read-Only Diff Viewer Reuse**: Reuses `DiffViewerModal` and its side-by-side / unified renderers with a `readOnly={true}` flag, disabling editing and acceptance controls to inspect file diffs safely.

---

# Studio File Explorer & Embedded Code Editor (Version 0.3)

Version 0.3 transforms the workspace studio into an integrated development environment:

- **Filesystem Management**: Extends `IFilesystemService` with safe file creation, renaming, moving, deletion, and content reading/writing. Strict workspace containment validation is enforced against every path argument to eliminate path traversal attacks.
- **Embedded Lightweight Editor**: An in-browser editor allowing developers to inspect and edit code without external IDEs.
  - Client-side syntax highlighting for popular programming languages.
  - Text search and replace (`Ctrl+F`).
  - Quick file navigation (`Ctrl+P`).
  - Read-only viewing mode toggle.
  - Inline diff comparison against saved disk state.

---

## Extensibility

The rendering system is designed to be extensible.

Future versions may introduce additional renderers, including:

- Microsoft Office documents
- Audio
- Video
- Jupyter Notebooks
- Mermaid diagrams
- PlantUML diagrams

Third-party plugins may register additional renderers without modifying the core application.

---

## Design Principles

The rendering architecture follows these principles:

- Server-side renderer selection
- Provider-independent rendering
- Plugin-friendly design
- Graceful fallback for unsupported content
- Separation between rendering logic and presentation

---

# Persistence

Business entities should not know how they are stored.

Persistence concerns belong exclusively to Infrastructure.

---

# Security Architecture

Security is enforced at multiple layers.

- Authentication
- Authorization
- Permission Validation
- Secret Encryption
- HTTPS

No sensitive action bypasses authorization.

## Single-User Account Management (Version 0.3)

In Version 0.3, the authenticated administrator can update their username and password directly from the server settings interface:
- Current password verification is strictly required for both username and password changes.
- Cryptographic salts and hashes are regenerated using Argon2id/PBKDF2.
- Session cookie claims are re-issued immediately to maintain the active session without unexpected logouts.
- Endpoints are rate-limited via `AuthLimiter`.

---

# Extensibility

New functionality should be added through extension points.

Examples:

- Providers
- File previews
- Plugins
- Importers
- Exporters

Existing code should require minimal modification.

---

# Dependency Rules

Dependencies always point inward.

```
Presentation

↓

Application

↓

Domain

↑

Infrastructure
```

The Domain Layer depends on nothing.

---

# Architectural Principles

The following principles should never be violated.

## API First

All features use the public API.

---

## Provider Agnostic

Business logic never depends on a specific AI provider.

---

## Server-Centric

Business logic remains on the Server.

---

## Single Responsibility

Every component should have one reason to change.

---

## Dependency Inversion

High-level modules never depend directly on implementation details.

---

## Explicit Dependencies

Avoid service locators and hidden dependencies.

Constructor injection should be preferred.

---

## Long-Term Evolution

The architecture should allow future support for:

- Multiple users
- Plugin system
- Mobile clients
- Native desktop shell
- Internet access
- Cloud synchronization

without redesigning the application core.

---

# Out of Scope

This document intentionally avoids describing:

- implementation details
- frameworks
- libraries
- coding standards
- testing strategy

These topics belong to:

- DevelopmentStandards.md
- ADRs