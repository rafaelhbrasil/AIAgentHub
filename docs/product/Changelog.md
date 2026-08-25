# AI Agent Hub

# Changelog

All notable changes to this project will be documented in this file.

The format is based on **Keep a Changelog** and follows **Semantic Versioning**.

## Versioning

The project follows Semantic Versioning:

```
MAJOR.MINOR.PATCH
```

Examples:

- 0.1.0
- 0.1.1
- 0.2.0
- 1.0.0

---

# Categories

Changes should be grouped using the following categories whenever applicable.

## Added

New functionality.

## Changed

Changes to existing functionality.

## Deprecated

Features scheduled for removal.

## Removed

Removed functionality.

## Fixed

Bug fixes.

## Security

Security-related improvements.

---

# [Unreleased]

## Added

- **GitHub Copilot Provider**: Added official GitHub Copilot CLI (`@github/copilot`, `copilot`) integration with dynamic CLI model discovery, authentication probe testing, and multi-turn session persistence.
- **Reasoning Effort Control**: Added model reasoning effort / thinking level configuration (`low`, `medium`, `high`, `max`) mapped to provider CLI flags (`--effort` for Antigravity, `--variant` for OpenCode) and exposed via UI header dropdown.
- **Dynamic CLI Model Discovery & Caching**: Added dynamic model listing from CLI (`opencode models`) with fallback catalogs, model caching, and forced refresh support (`GET /api/v1/providers/{id}/models?refresh=true`).
- **Provider Status Monitoring**: Added real-time detailed provider status endpoint (`GET /api/v1/providers/{id}/status?refresh=true`) with enum status indicators (`Ready`, `NotInstalled`, `Unauthenticated`, `QuotaExceeded`, `Error`, `Running`).
- **Per-Conversation CLI Session Tracking**: Implemented `ProviderSessionId` persistence per conversation to isolate CLI agent sessions across chats (`agy --conversation <id>`, `opencode run --session <id>`).
- **EF Core Code-First Migrations**: Introduced EF Core Code-First Migrations (`Microsoft.EntityFrameworkCore.Migrations`) with automated runtime migration (`Database.MigrateAsync()`) on application startup.
- **PowerShell Execution Modes**: Enhanced execution engine to support both headless streaming and visible PowerShell windows in non-headless mode.
- **Universal Headed CLI Execution**: When Headed mode is enabled (`Headless: false`), all auxiliary provider commands (dynamic model discovery, status/version detection, authentication checks, and session listings) execute via visible desktop PowerShell console windows instead of silent background processes.
- **SPA Client-Side Routing**: Added browser history and client-side URL hash routing (`#workspace/...`, `#conversation/...`).

- **Responsive Mobile & Tablet Design**: Overhauled all application views with multi-breakpoint media queries, replacing desktop navbar on mobile with a compact hamburger drawer menu, compact server status indicator, and mobile layout switcher.
- **Compact Studio Header & Action Options**: Consolidated workspace and conversation headers into a single 42px topbar with an Options dropdown menu, maximizing vertical chat space.
- **Auto-Growing Prompt Textbox & Circular Send Button**: Textarea dynamically expands up to 30% of screen height with a dedicated circular send button. On mobile devices, Enter inserts newlines while Shift+Enter/Enter behavior is preserved on desktop.
- **User Cancellation Notice on Abort**: Explicitly records and broadcasts `*(AI response was cancelled by the user.)*` when an ongoing execution is cancelled, saving it to persistent message history.
- **Single-Click Visual Folder Navigation**: Replaced double-click folder navigation with single-click directory traversal in the folder explorer modal, dynamically defaulting to the user's home profile directory.

## Fixed

- Fixed provider status showing "Unknown" by enabling global string enum serialization in ASP.NET Core (`JsonStringEnumConverter`).
- Fixed OpenCode "Session not found" error by tagging initial runs with `--title agenthub-{conversationId}` and resolving generated native IDs (`ses_...`).
- Fixed active model count not updating on provider cards after closing the model configuration modal by syncing provider state and fetching fresh DB model catalogs.
- Fixed returning from conversation studio requiring two clicks on the Back button due to URL synchronization race condition.
- Fixed options dropdown menu clipping behind the conversation message pane on mobile by correcting stacking context z-indexes.

# [0.1.0] - Initial MVP

**Release Date:** TBD

The first public release establishes the architectural foundation of AI Agent Hub.

The primary objective of this release is to provide a complete single-user experience capable of orchestrating multiple AI coding agents through a unified, provider-agnostic interface.

For the complete functional specification, see:

- `docs/product/releases/Release-v0.1.md`

## Added

### Product

- Initial AI Agent Hub MVP.
- Provider-agnostic architecture.
- Server-centric execution model.
- Browser-based local and remote interface.

### Provider Management

- Automatic provider detection (including Google DeepMind Antigravity CLI `agy`, Gemini CLI, OpenAI Codex CLI, Claude Code, and OpenCode).
- Guided provider installation.
- Guided authentication.
- Model discovery and real-time streaming tokens.
- Model selection.
- Provider capability detection.

### Workspace Management

- Workspace creation with interactive Windows-style visual folder navigator (Quick Access shortcuts, Breadcrumbs address bar, folder tile grid, and native folder picker integration).
- Workspace opening and drive selection.
- Workspace explorer.
- Persistent Workspace settings.
- Global application settings.

### Conversations

- Persistent conversations.
- Conversation history.
- Conversation rename.
- Conversation deletion.
- Conversation search.
- Markdown rendering.
- Syntax highlighting.
- Streaming responses.
- Code block rendering.

### AI Execution

- Agentic file access.
- Permission requests.
- Execution log.
- MCP support.
- Skills support.
- Provider-specific capabilities.

### File Management

- File change detection.
- Side-by-side diff viewer.
- Unified diff viewer.
- Accept changes.
- Reject changes.
- File change history.

### File Preview

Support for previewing:

- Markdown
- Images
- Plain text
- JSON
- XML
- YAML

### Server

- HTTPS support.
- Configurable listening port.
- Localhost mode.
- LAN mode.
- Selected network interface mode.
- Single administrator account.
- Secure session management.
- Encrypted provider credentials.

### Remote Station

- Browser-based access.
- Shared Workspaces.
- Shared conversations.
- Shared AI execution.
- File preview.
- Diff review.

### Security

- Password hashing.
- Secret encryption.
- HTTPS communication.
- Permission validation.
- Secure authentication.

### Documentation

Initial project documentation including:

- Product documentation.
- Roadmap.
- Release documentation.
- Architecture.
- Technical standards.
- Repository structure.
- Security architecture.
- API reference.
- Glossary.
- Contribution guidelines.
- Architecture Decision Records.

## Security

Initial security architecture introduced.

Highlights include:

- Mandatory authentication.
- Password hashing.
- Encrypted provider secrets.
- HTTPS communication.
- Permission-based AI operations.

---

# Migration Notes

## 0.1.0

Initial public release.

No migration is required.

---

# References

Product vision:

- `/docs/Product/Product.md`

Roadmap:

- `/docs/Product/Roadmap.md`

Release specifications:

- `/docs/Product/Releases/`

Architecture:

- `/docs/Technical/Architecture.md`

Architecture Decision Records:

- `/docs/Technical/ADR/`