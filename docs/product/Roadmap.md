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

Future versions are intentionally described at a higher level since priorities may evolve over time.

---

# Product Evolution

The development of AI Agent Hub is divided into several phases.

Each phase expands the platform while preserving the architectural principles defined in **Product.md**.

---

# Phase 1 — Foundation

**Release:** Version 0.1

Objective:

Deliver a production-ready single-user experience capable of orchestrating multiple AI coding agents through a unified interface.

Primary focus:

- Provider abstraction
- Workspace management
- Conversations
- AI execution
- Diff viewer
- File preview
- Remote browser access
- Security
- Server architecture

See:

> `docs/product/releases/Release-v0.1.md`

---

# Phase 2 — Developer Experience

**Release:** Version 0.2

Objective:

Improve day-to-day productivity without replacing a professional IDE.

Primary focus:

- Git integration
- Embedded editor
- File explorer
- Prompt library
- Provider comparison
- Conversation improvements
- Analytics
- Better UI

See:

> `docs/product/releases/Release-v0.2.md`

---

# Phase 3 — Collaboration

**Target:** Version 0.3

Primary goals:

- Multiple users
- Roles
- Permissions
- Audit log
- Workspace sharing
- Snapshot mode
- Synchronization mode

At this stage AI Agent Hub evolves from a personal assistant into a collaborative development platform.

---

# Phase 4 — Extensibility

Primary goals:

- Plugin system
- Provider SDK
- Theme SDK
- Preview extensions
- Third-party integrations

The objective is to make AI Agent Hub extensible without modifying the application core.

---

# Phase 5 — Enterprise

Potential features include:

- Enterprise authentication
- MFA
- External identity providers
- Advanced administration
- Certificate management
- Usage policies
- Centralized configuration

The exact scope remains intentionally undefined.

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

## Desktop Integration

- Run the app executable in a system tray icon
- Tray menu options: open web interface, view logs, exit app
- If technically possible, add a tray option to restart the backend service
- Log to a file per day at `./logs/yyyy-mm-dd.log` (same folder where the app runs)
- "View logs" option opens the log folder

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

## File Explorer

- Allow the user to add new items to the folder structure (like VS Code explorer)
- Operations: new folder, new file, rename file, delete file/folder, collapse all folders

## Conversation Switching

- Allow swapping between two or more conversations without blocking the prompt text box
- In-progress processing continues in the background
- User can switch back and forth and still abort an ongoing task

## Diff Viewer

- Add a word-wrap toggle button when diffing a file

## Mentions

- Support `@<file>` or `@<folder>` mentions (similar to Aider and OpenCode)
- While typing, filter any file/folder within the workspace scope
- Once the user picks an item, replace `@<search>` with `$/path/to/file.ext`

---

# Deferred Ideas

The following ideas remain intentionally outside the current roadmap.

They may become future projects or optional modules.

- Cloud-hosted Server
- Marketplace
- Collaborative editing
- Distributed AI execution
- Telemetry dashboard
- Subscription management
- Billing
- Team analytics

Their inclusion depends on future project maturity.