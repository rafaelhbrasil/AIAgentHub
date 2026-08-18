
# Version 0.1 — Minimum Viable Product

## Objectives

Version 0.1 delivers the first complete, usable product.

The objective is **not** to implement every planned feature.

Instead, the goal is to validate the core architecture and provide a complete workflow from installation to AI-assisted development.

The MVP should already demonstrate all major concepts of the platform while keeping the implementation intentionally simple.

The architecture introduced in v0.1 must be capable of supporting future versions without significant redesign.

---

# Provider Management

## Supported Providers

The application should automatically detect compatible providers installed on the machine.

Initially supported providers include:

- Antigravity CLI (`agy`) — Google DeepMind
- Gemini CLI
- Codex CLI
- Claude Code
- OpenCode

The architecture must allow adding additional providers without modifying the application core.

---

## Provider Detection & Caching

The application shall:

- scan known installation locations and system PATH
- verify executable availability (including platform binaries and aliases)
- determine provider version
- determine provider capabilities
- detect supported models
- persist detection status and model lists in the database cache (SQLite)
- serve provider metadata and model lists strictly from the database without in-memory or client-side caching; external provider CLI checks and model listings are only executed when unseeded or when the user explicitly requests a Refresh or Refresh All

---

## Provider Installation

When a provider is not installed, the application should assist the user by:

- explaining what is missing
- providing installation guidance
- opening official documentation
- copying official installation commands when applicable

The application should never rely on unofficial installation methods.

---

## Authentication

The application should guide users through the provider's official authentication process.

Each installed provider that is not yet authenticated displays an "Authenticate" button. Clicking it opens the provider's native CLI for authentication (e.g., a PowerShell/terminal window running the provider's own auth command).

The application should never collect credentials directly or implement unofficial authentication mechanisms.

---

## Models

Users should be able to:

- list available models
- select a default model (when "Default Model" is chosen, the `--model` flag is omitted so the CLI delegates to its internally configured or upstream default model)
- override the model per conversation via an active model dropdown in the conversation header
- configure reasoning effort where supported by the provider (e.g. low, medium, high)

---

## Provider Capabilities

Each provider should expose its capabilities.

Examples:

- MCP support
- Skills
- Tool support
- Streaming
- Multi-model support

The UI should adapt accordingly.

---

# Workspace Management

## Workspace Lifecycle

Users should be able to:

- create Workspaces
- open existing Workspaces
- remove Workspaces
- reopen recent Workspaces

### Create Workspace Dialog

The dialog asks for the folder first using an interactive Windows-style visual folder browser dialog:
- Quick Access shortcuts (Code Projects, User Home, Desktop, Documents, Downloads, ready Drives/Partitions)
- Clickable Breadcrumb address bar with "Up" navigation
- Interactive folder tile grid with single-click selection and double-click navigation
- Native browser folder picker integration (`webkitdirectory`)

The folder browser allows browsing drives and directories on the Server filesystem via a backend API:
- `GET /api/v1/filesystem/drives` — list available drives
- `GET /api/v1/filesystem/browse?path=...` — list subdirectories

When the user selects a folder, the application suggests a name based on the last directory component.

Example: path `D:\Code\ai\AgentHub` (with or without trailing slash) suggests name `AgentHub`.

---

## Workspace Explorer

Each Workspace should expose:

- folder tree
- files
- provider configuration
- conversations

---

## Workspace Settings

Settings should include:

- default provider
- default model
- ignored files
- Workspace-specific configuration

---

# Conversation Management

Users should be able to:

- create conversations
- rename conversations
- delete conversations
- search conversations

Conversations should remain persistent.

---

## Conversation Rendering

Supported features include:

- Markdown rendering
- syntax highlighting
- code blocks
- tables
- images
- copy buttons
- streaming responses

---

# AI Execution

## Agentic File Access

AI providers should be capable of reading and modifying project files.

In Version 0.1 all actions are auto-approved (see Permissions below).

---

## Permissions

Version 0.1 operates in **auto mode**.

All provider actions (file edits, file creation, file deletion, command execution) are automatically approved without user confirmation.

The permission request system is deferred to Version 0.2.

---

## Execution Log

The application should maintain an execution log showing:

- provider
- model
- timestamp
- requested action
- execution result

---

# MCP Management

Version 0.1 should fully support MCPs available through each provider.

Capabilities include:

- discovery
- listing
- enable/disable
- Workspace configuration

---

# Skills

Version 0.1 should expose provider Skills whenever supported.

Capabilities include:

- listing
- enable/disable
- Workspace-specific configuration

---

# File Changes

The application should automatically detect modifications produced by AI providers.

Change detection is provider-agnostic. The Server captures a snapshot of the affected Workspace before an AI execution and compares it with the Workspace after execution to identify created, modified and deleted files. File system watchers may be used as an optimization, but snapshot comparison is the authoritative mechanism.

---

## Diff Viewer

Supported modes:

- side-by-side diff (primary)
- unified diff

UI Layout & Interaction:

- A bottom panel displays the list of affected files for fast selection.
- Side-by-side diff is the primary view mode.
- For Markdown files, a 3-pane split view is supported (original content, modified content, and a Preview tab displaying the rendered new content).
- For Image files, a visual side-by-side comparison displays previous and new images directly.

Users should be able to:

- inspect changes
- accept changes
- reject changes

Diffs are generated on demand by comparing the original snapshot with the current file contents. Both views are produced by a common diff engine.

---

## Accept and Reject

- **Accept** marks a change as reviewed, no file transformation is performed, and the baseline snapshot for that file is reset to the current accepted state for subsequent change detection cycles.
- **Reject** restores the affected file(s) from the pre-execution snapshot.

On mobile devices, comparison modals display in full-screen mode with background scroll locking and scroll containment to prevent background chat scrolling during diff review.

This mechanism is independent of Git and must work for any project.

---

## Change History

Each conversation should expose its file modifications.

Version 0.1 maintains a per-conversation change history recording which files were affected by each AI execution. This history supports review and rollback; it is not a full version control system.

Persistent metadata (conversation, affected files, timestamps, execution metadata) is stored in SQLite. Original file snapshots required for rollback are stored on disk in an internal application data directory. Diffs are transient artifacts generated on request and are not persisted.

---

# File Preview

The MVP should support previewing common file types directly inside the application.

Supported formats include:

- Markdown
- PNG
- JPG
- GIF
- SVG
- TXT
- JSON
- XML
- YAML

The preview system should be extensible.

---

# Server

Version 0.1 introduces the Server architecture.

The Server owns:

- Workspaces
- Providers
- Conversations
- MCPs
- Skills
- Authentication
- Configuration

---

## Network Configuration

Supported modes:

### Localhost
Only accessible from the local machine. The server strictly enforces loopback origin (127.0.0.1 / ::1) via request pipeline middleware, returning HTTP 403 Forbidden to any non-loopback clients.

---

### LAN
Accessible through every network interface. All local network interfaces and connected LAN clients are accepted.

---

### Selected Interfaces
Accessible only through loopback and explicitly selected network interfaces. Requests originating outside loopback and the whitelisted interfaces are rejected with HTTP 403 Forbidden.

The UI should display:

- interface name
- IP address
- current status
- selection checkboxes when in Selected Interfaces mode

---

## Security

Version 0.1 supports:

- one administrator account
- HTTPS
- password hashing
- encrypted provider credentials
- session management

### First Run & Setup

On first launch, when no administrator account exists, the application enters **Setup Mode**.

The setup wizard:

- prompts for username and password (entered twice for confirmation)
- creates the administrator account
- automatically authenticates the user (no separate login step required after setup)
- displays a recovery code that must be saved for password recovery

Subsequent launches require username/password authentication.

### Persistent Authentication

Login creates a persistent cookie valid for 30 days with sliding expiration.

Closing and reopening the browser does not require re-authentication as long as the cookie remains valid.

### Password Recovery

A recovery code is generated on first startup and displayed:

- during setup completion
- in the application console on every startup
- on the Settings page for authenticated administrators

From the login page (localhost only), the user may enter the recovery code to reset all data and return to Setup Mode.

If the recovery code is lost, the user must manually delete the data directory (`%LocalAppData%\AIAgentHub`) and restart the application.

### Default Port & Port Overrides

The Server listens on port 5432 (HTTPS) and 5433 (HTTP) by default. The listening URLs and ports can be overridden using `--urls "<url>"`, `--port <port>`, or the `ASPNETCORE_URLS` environment variable (e.g., `--urls "https://localhost:5001"` or `--port 5001`).

### HTTPS Certificates

Version 0.1 provides HTTPS through an automatically generated **self-signed certificate** (created on first launch).

Operator-supplied certificates, internal/trusted CAs and reverse-proxy TLS termination are deferred to Version 0.2 (see Release-v0.2.md §Operator-supplied Certificates and SecurityArchitecture.md §HTTPS).

---

# Remote Station

Remote Stations access the Server through a browser.

Capabilities include:

- browse Workspaces
- browse conversations
- preview files
- review diffs
- execute AI tasks

Remote Stations never execute AI providers locally.

---

# User Interface

The MVP should provide a clean and predictable interface.

Major sections include:

- Dashboard
- Workspaces
- Conversations
- Providers
- MCPs
- Skills
- Settings

---

# Search

Version 0.1 includes search for:

- Workspaces
- Conversations

---

# Configuration

Global configuration should include:

- providers
- networking
- authentication
- appearance
- logging

Workspace configuration should include:

- default provider
- model
- ignored files

---

# Logging

The application should log:

- startup
- provider detection
- authentication
- AI execution
- errors

Specific logging levels, message format and retention policy are intentionally deferred for Version 0.1 and will be defined during implementation (see DevelopmentStandards.md §Logging).

---
