# AI Agent Hub

# Roadmap

**Version:** 0.1 Draft

---

# Purpose

This document provides a high-level overview of the planned evolution of AI Agent Hub.

Detailed requirements for each release are maintained in dedicated documents.

Current planning documents:

- `docs/product/releases/Release-v0.1.md`
- `docs/product/releases/Release-v0.2.md`
- `docs/product/releases/Release-v0.3.md`
- `docs/product/releases/Release-v0.4.md`
- `docs/product/releases/Release-v0.5.md`
- `docs/product/releases/Release-v0.6.md`

Future versions are intentionally described at a higher level since priorities may evolve over time.

---

# Product Evolution

The development of AI Agent Hub is divided into sequential minor releases.

Each release expands the platform while preserving the architectural principles defined in **Product.md**.

---

# Phase 1 — Foundation

**Release:** Version 0.1

Objective: Deliver a production-ready single-user experience capable of orchestrating multiple AI coding agents through a unified interface.

Primary focus:
- Provider abstraction
- Workspace management
- Conversations & message history
- AI execution engine
- Side-by-side & unified diff viewer
- File previews
- Remote browser access
- HTTPS & security architecture
- Server architecture

See:
> `docs/product/releases/Release-v0.1.md`

---

# Phase 2 — Multi-Provider Flexibility & Chat DX

**Release:** Version 0.2

Objective: Deliver seamless multi-provider orchestration flexibility and chat developer experience.

Primary focus:
- In-conversation AI provider switching
- Context migration & differential replay protocol
- N-to-N session tracking (`ConversationProviderSession`)
- Chat input autocomplete for Skills (`/`) and file/folder mentions (`@`)
- Dedicated provider settings modal, model configuration & visibility controls
- Folder creation directly within workspace navigator
- Themes (Dark, Light, System)

See:
> `docs/product/releases/Release-v0.2.md`

---

# Phase 3 — Workspace Developer Tools & Git

**Release:** Version 0.3

Objective: Expand AI Agent Hub into a self-sufficient developer workspace environment.

Primary focus:
- Native Git integration (status, branch switch, commit, push, pull, stash, history)
- Git repository cloning directly into workspace
- Studio file explorer (create, rename, delete files/folders, drag & drop)
- Embedded lightweight code editor (syntax highlighting, quick edits, search, go-to-file)
- Expanded file previews (PDF, HTML, CSV, LOG, INI, TOML)

See:
> `docs/product/releases/Release-v0.3.md`

---

# Phase 4 — Multi-Pane Studio & Productivity

**Release:** Version 0.4

Objective: Empower power users with advanced productivity layouts, parallel execution, and reusable prompt engineering.

Primary focus:
- Studio multi-pane split layouts (dual chats, chat + editor, chat + live diff)
- Multi-provider parallel prompt execution and side-by-side comparison
- Command Palette (`Ctrl+K`) and comprehensive keyboard shortcuts
- Reusable Prompt Library (templates with `{{variables}}`, categories, import/export)

See:
> `docs/product/releases/Release-v0.4.md`

---

# Phase 5 — Ecosystem, Analytics & Server Operations

**Release:** Version 0.5

Objective: Expand tool ecosystem integration, local usage analytics, and server operations.

Primary focus:
- Cross-provider skill sharing via filesystem symlinks and directory junctions
- Advanced MCP Server lifecycle management and startup options
- 100% local usage analytics and cost estimation (zero external telemetry)
- Operator-supplied HTTPS certificates (PFX, Windows cert store, ACME / Let's Encrypt)
- Server backup, restore, and portability export
- Application and CLI update checker

See:
> `docs/product/releases/Release-v0.5.md`

---

# Phase 6 — Collaboration & Multi-User

**Release:** Version 0.6

Objective: Transform AI Agent Hub into a collaborative, team development platform.

Primary focus:
- Multi-user authentication and Role-Based Access Control (RBAC)
- Per-workspace permissions and provider access policies
- Security audit logging and active session management
- Remote Station workspace sharing (Snapshot and Synchronization modes)

See:
> `docs/product/releases/Release-v0.6.md`

---

# Phase 7 — Extensibility & Plugins

**Target:** Version 0.7+

Primary goals:
- Plugin system
- Provider SDK
- Theme SDK
- Preview extensions
- Third-party integrations

---

# Phase 8 — Enterprise & Stable Public Release

**Target:** Version 1.0.0

Potential features include:
- Enterprise authentication (SSO / OIDC / SAML)
- Multi-factor authentication (MFA)
- Enterprise audit compliance and retention policies
- Distributed AI execution options

---

# Long-Term Vision

The long-term objective is to establish AI Agent Hub as the reference platform for AI coding agents.

The platform should become:

- provider agnostic
- API first
- server centric
- extensible
- secure

Future AI providers should integrate naturally into the platform without requiring users to learn new workflows.

---

# Planned Documentation

The documentation evolves alongside the project.

Current documentation includes:

- Product.md
- Roadmap.md
- Architecture.md
- DomainModel.md
- DevelopmentStandards.md
- RepositoryStructure.md
- SecurityArchitecture.md
- ApiDesign.md
- Glossary.md
- ContributingGuide.md

Architecture decisions are documented separately under:

```
docs/technical/adr/
```

---

# Release Strategy

Each release must satisfy the following goals before progressing to the next phase:

- Stable
- Documented
- Tested
- Backward compatible whenever possible

The project prioritizes long-term maintainability over rapid feature growth.

---

# Versioning

The project follows Semantic Versioning.

```
0.x.x

Experimental

↓

1.0.0

Stable Public Release
```

Minor releases expand functionality.

Patch releases focus on fixes and improvements.

Major releases may introduce carefully planned architectural evolution.

---

# Future Ideas

The following ideas are candidates for future releases. They are not yet assigned to a specific phase.

## Desktop Integration & Hidden Console Mode

- **Hidden Console Daemon**: Run the application executable silently in the background on desktop launch without an open console window.
- **System Tray Icon**: Persistent icon in the taskbar notification area.
- **Tray Context Menu**:
  - *Open Web Interface* (opens default browser to `https://localhost:5432` or configured port)
  - *View Logs* (opens the daily logs folder)
  - *Restart Backend Server* (gracefully restarts the server daemon)
  - *Exit App* (terminates the background daemon cleanly)
- **Daily Rolling File Logs**: Rolling log files stored per day at `./logs/yyyy-mm-dd.log` (in the application execution directory).

## Service Management

- Allow the user to restart the backend service from the web interface
- Requires authenticated session

## Chat UX

- Add a small copy button (icon only) to every message in the chat window
- One-click copy without needing to select text

## File & Image Attachments

- Allow attaching external files and images directly to chat prompts (via paperclip button, file picker, drag-and-drop, or clipboard paste into the prompt area)
- Display attachment chips and image thumbnails in the prompt input bar prior to submission
- Pass attached file contents or image data to compatible multimodal AI providers and CLI orchestrations

## Conversation Switching

- Allow swapping between two or more conversations without blocking the prompt text box
- In-progress processing continues in the background
- User can switch back and forth and still abort an ongoing task

## Diff Viewer

- Add a word-wrap toggle button when diffing a file

---

# Deferred Ideas

The following ideas remain intentionally outside the current roadmap.

They may become future projects or optional modules.

- Workspace Templates (predefined project scaffolding layouts)
- Cloud-hosted Server
- Marketplace
- Collaborative editing
- Distributed AI execution
- Telemetry dashboard
- Subscription management
- Billing
- Team analytics

Their inclusion depends on future project maturity.