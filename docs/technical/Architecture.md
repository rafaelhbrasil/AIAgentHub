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

---

## Provider Layer

Each AI provider is implemented as an adapter inheriting from `CliProviderBase`. Providers delegate all process execution to the injected `IProcessExecutor`.

When building CLI arguments, if a conversation is set to "Default Model" (or if `ModelId` is empty, `"default"`, `"auto"`, etc.), provider adapters omit the `--model` CLI argument completely to let the underlying CLI resolve to its own configured or upstream default model.

Examples:

- CodexProvider
- GeminiProvider
- ClaudeProvider
- OpenCodeProvider
- AntigravityProvider

The remainder of the application communicates only through abstractions.

---

## Background Services

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
└── OpenCodeProvider
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

## Unsupported Files

If no renderer is available, the application should:

- Offer file download.
- Display basic file information.
- Never fail the request.

---

## Diff Rendering

The rendering architecture is also used for file differences.

Version 0.1 supports:

- Unified diff
- Side-by-side diff

Diff rendering is independent from preview rendering but shares the same rendering infrastructure.

---

## Extensibility

The rendering system is designed to be extensible.

Future versions may introduce additional renderers, including:

- PDF
- CSV
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