# AI Agent Hub

# Domain Model

**Version:** 0.1 Draft

---

# Purpose

This document defines the core domain concepts of AI Agent Hub.

Its purpose is to describe the business model independently from implementation details.

This document intentionally does **not** define:

* C# interfaces
* Entity Framework entities
* Database schema
* REST DTOs
* Serialization formats

Those are implementation concerns and may evolve without changing the domain model.

---

# Domain Overview

AI Agent Hub is organized around a small number of core concepts.

```text
Workspace
    │
    ├── Conversations
    │       │
    │       ├── Messages
    │       └── File Changes
    │
    ├── Provider Configuration
    │
    ├── Skills
    │
    └── MCP Servers
```

Every interaction occurs within a Workspace.

---

# Core Concepts

## Workspace

A Workspace represents a software project managed by AI Agent Hub.

A Workspace is the primary boundary for user interactions.

### Responsibilities

* Represents a project.
* References a project directory.
* Owns conversations.
* Stores workspace-specific settings.
* Associates providers, Skills and MCP servers.
* Maintains project history.

### Relationships

```text
Workspace

├── Conversation
├── Provider Configuration
├── Skills
└── MCP Servers
```

---

## Conversation

A Conversation represents an AI interaction session.

A Conversation always belongs to exactly one Workspace.

### Responsibilities

* Stores chat history.
* Maintains message order.
* Tracks the active provider.
* Tracks the active model.
* Tracks reasoning effort / thinking level setting (`Effort`).
* Persists external CLI session mapping (`ProviderSessionId`).
* Records execution metadata.

### Relationships

```text
Workspace

└── Conversation

        ├── Messages

        └── File Changes
```

---

## Message

A Message represents a single interaction within a Conversation.

Messages may originate from:

* User
* AI Provider
* System

### Responsibilities

* Store message content.
* Preserve chronological order.
* Store timestamps.
* Reference generated artifacts.
* Reference file changes.

---

## Provider

A Provider represents an AI execution engine.

Examples include:

* Antigravity CLI (`agy`) — Google DeepMind
* Gemini CLI
* OpenAI Codex CLI
* Claude Code
* OpenCode

Providers are replaceable and isolated behind a common abstraction.

### Responsibilities

* Discover available models.
* Authenticate users.
* Execute prompts.
* Stream responses.
* Expose capabilities.
* Request permissions.
* Report operational lifecycle status (`NotInstalled`, `Unauthenticated`, `Ready`, `Error`, `Running`, `QuotaExceeded`, `Discontinued`).
* Manage provider-specific configuration.

---

## Model

A Model represents an executable AI model exposed by a Provider.

Examples:

* GPT-5.5
* Gemini 2.5 Pro
* Claude Opus
* Qwen Coder

### Responsibilities

* Describe execution capabilities.
* Expose provider-specific metadata.
* Support model selection.
* Track user visibility state (`IsDisplayed` toggle flag, default `true`).
* Reconcile model availability during provider detection/refresh (purge deleted, add new as enabled, preserve existing toggles; the implicit "Default Model" is always available, cannot be toggled off, and is not stored in the database).

---

## Capability

A Capability represents a feature supported by a Provider or Model.

Examples include:

* Streaming
* Tool Calling
* Vision
* Image Generation
* MCP
* Skills
* File Editing
* Reasoning

Capabilities allow the application to adapt dynamically to different providers.

---

## Skill

A Skill extends the behavior of an AI Provider.

Skills are provider-independent whenever possible.

### Responsibilities

* Provide reusable instructions.
* Encapsulate domain knowledge.
* Improve task execution.
* Be enabled or disabled per Workspace.

---

## MCP Server

An MCP Server provides external tools and resources.

### Responsibilities

* Expose tools.
* Expose resources.
* Expose prompts.
* Extend provider capabilities.

The application manages MCP server configuration but does not implement the MCP protocol itself.

---

## File Change

A File Change represents a modification proposed or performed by an AI Provider.

### Responsibilities

* Reference affected files.
* Store change metadata.
* Support preview.
* Support diff visualization.
* Support acceptance.
* Support rejection.
* Support rollback.

---

## Permission Request

A Permission Request represents an action requiring user approval.

Examples include:

* Reading files.
* Writing files.
* Executing commands.
* Network access.

### Responsibilities

* Pause provider execution.
* Wait for user decision.
* Resume or cancel execution.

---

## Provider Configuration

Provider Configuration stores workspace-specific provider settings.

Examples include:

* Preferred provider.
* Preferred model.
* Provider-specific options.

Provider Configuration belongs to a single Workspace.

---

# Aggregate Boundaries

The following aggregates define ownership boundaries.

## Workspace Aggregate

Owns:

* Conversations
* Provider Configuration
* Skills
* MCP configuration

---

## Conversation Aggregate

Owns:

* Messages
* File Changes

---

## Provider Aggregate

Owns:

* Models
* Capabilities

Provider implementations remain isolated from the remainder of the domain.

---

# Relationships

```text
Workspace (1)

├── Conversation (0..*)

│       ├── Message (1..*)

│       └── File Change (0..*)

│
├── Provider Configuration (1)

├── Skill (0..*)

└── MCP Server (0..*)
```

---

# Design Principles

The domain model follows these principles.

## Provider Independence

Business logic must never depend on a specific AI provider.

---

## Workspace Isolation

Every conversation belongs to exactly one Workspace.

Workspaces do not share state.

---

## Explicit Ownership

Every domain object has a clear owner.

Ownership simplifies persistence, synchronization and authorization.

---

## Extensibility

New providers, capabilities, Skills and MCP servers should be added without changing the existing domain model.

---

## Separation of Concerns

The domain model describes business concepts only.

Implementation details belong elsewhere, including:

* Infrastructure
* Persistence
* REST API
* OpenAPI
* Entity Framework Core
* Provider adapters

---

# Related Documentation

* Product/Product.md
* Technical/Architecture.md
* Technical/ProviderArchitecture.md *(future)*
* Technical/ApiDesign.md
* Technical/ADR/
