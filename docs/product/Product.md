# AI Agent Hub

# Product Vision & Specification

**Version:** 0.1 Draft  
**Status:** Work in Progress  
**Audience:** Product Owners, Developers, Contributors, Architects

---

# 1. Executive Summary

AI Agent Hub is a self-hosted application that provides a unified graphical interface for AI coding agents.

Instead of interacting directly with multiple command-line tools such as Antigravity CLI, Codex CLI, Gemini CLI, Claude Code, OpenCode and future providers, users interact with a single, consistent interface capable of orchestrating all supported AI agents.

The application is **provider-agnostic**.

It does not replace AI providers. Instead, it standardizes their user experience while preserving each provider's native capabilities whenever possible.

The application is designed around the idea that AI coding agents should become interchangeable without forcing developers to learn different workflows for every vendor.

---

# 2. Vision

AI Agent Hub aims to become the standard desktop environment for AI coding agents.

Just as Visual Studio Code became the common editor for dozens of programming languages, AI Agent Hub should become the common interface for multiple AI providers.

The application should never favor a specific vendor.

Users should be free to choose the provider that best fits each task while maintaining the same workflow.

The long-term objective is to separate the **user experience** from the **AI implementation**.

---

# 3. Mission

Provide the best possible user experience for interacting with AI coding agents.

The application should simplify setup, improve productivity, increase transparency and reduce vendor lock-in while remaining fully compatible with each provider's native capabilities.

---

# 4. Problem Statement

Today's AI coding agents evolved independently.

Each provider has its own:

- CLI
- authentication flow
- configuration files
- permissions model
- project management
- conversation history
- update mechanism
- installation process

As developers evaluate multiple providers they must repeatedly learn different interfaces, workflows and configuration models.

Many useful capabilities are also missing from current CLIs, including:

- visual file diffs
- centralized project management
- unified permissions
- consistent conversation management
- remote access
- provider-independent configuration

AI Agent Hub exists to solve these problems.

---

# 5. Goals

The project has the following primary goals.

## 5.1 Unified Experience

Provide one consistent user interface regardless of the selected AI provider.

---

## 5.2 Provider Independence

Avoid coupling the application to any specific vendor.

Every provider should integrate through adapters.

---

## 5.3 Preserve Native Features

Whenever possible, provider-specific capabilities should remain available.

Examples include:

- MCP support
- Skills
- provider tools
- model selection
- custom capabilities

The application should avoid reducing providers to the lowest common denominator.

---

## 5.4 Simplified Setup

Reduce the effort required to start using AI agents.

The application should assist users in:

- detecting installed providers
- installing missing providers
- authenticating providers
- configuring projects

---

## 5.5 Better Developer Experience

Provide capabilities that current CLIs generally do not offer.

Examples include:

- visual diffs
- project explorer
- file preview
- unified conversations
- centralized permissions
- remote access
- workspace management

---

# 6. Non-Goals

AI Agent Hub is **not** intended to become:

- an LLM
- an inference engine
- a model trainer
- a cloud AI provider
- a Git hosting platform
- an IDE replacement
- a package manager
- a remote desktop application
- a cloud synchronization platform

The project enhances existing AI providers.

It does not compete with them.

---

# 7. Target Audience

Primary audience:

- software developers
- DevOps engineers
- technical architects
- AI enthusiasts

Secondary audience:

- software teams
- consulting companies
- organizations operating self-hosted AI environments

---

# 8. Product Overview

AI Agent Hub consists of a single executable running as a **Server**.

The Server hosts:

- Backend
- REST API
- WebSocket API (SignalR)
- Web UI
- AI Providers
- Project Storage
- Conversation Storage

Users interact exclusively through the Web UI.

The same interface is used locally and remotely.

There is no separate desktop application for remote users.

---

# 9. Core Concepts

## Server

The Server is responsible for executing AI agents.

It owns:

- projects
- conversations
- installed providers
- provider configuration
- authentication
- permissions
- Git integration
- MCPs
- Skills

---

## Remote Station

A Remote Station is any browser connected to a Server.

A Remote Station never executes AI providers locally.

All AI execution happens on the Server.

---

## Workspace

A Workspace represents a managed software project.

Each Workspace contains:

- source code
- conversations
- provider configuration
- permissions
- execution history

Future versions may allow Workspaces originating from Remote Stations.

---

## Conversation

A persistent interaction between users and AI providers.

Conversations belong to a Workspace.

---

## Provider

A software component capable of communicating with one AI system.

Examples include:

- Antigravity CLI (`agy`) — Google DeepMind
- Gemini CLI
- Codex CLI
- Claude Code
- OpenCode

Additional providers should be installable without modifying the application core.

### Provider Model Visibility & Reconciliation

Providers expose lists of available models (e.g., OpenCode exposes large catalogs). Instead of cluttering provider cards with expansive model lists:

- Cards summarize available models via a model count link (e.g., "X models available").
- Opening the model modal presents a searchable list of models with visibility toggles (ON = displayed, OFF = hidden).
- Default visibility for newly discovered models is ON (displayed).
- Refreshing a provider reconciles model configurations:
  - Models no longer reported by the provider are purged.
  - Newly added models are inserted with default visibility (ON).
  - Existing models maintain their configured visibility setting (ON or OFF).
- Only models marked as displayed (toggle ON) appear in conversation model selectors and workspace settings.

---

# 10. Product Principles

## Provider Agnostic

The application must never assume a specific AI provider.

Every provider should integrate through the same abstraction layer.

---

## Preserve Native Capabilities

If a provider supports advanced functionality, the application should expose it whenever practical.

The objective is to unify the experience without limiting provider-specific features.

---

## API First

Every feature available through the UI must use the same public API.

The local UI consumes exactly the same REST and WebSocket (SignalR) endpoints used by Remote Stations.

No privileged execution path exists for localhost.

---

## Server-Centric Architecture

Business logic belongs to the Server.

Remote Stations remain lightweight clients.

This enables:

- browser-based access
- future native clients
- mobile clients
- API integrations

without duplicating business logic.

---

## Security First

The application follows the principle of least privilege.

Permissions are explicit.

Passwords are hashed.

Secrets are encrypted.

HTTPS is always used.

---

## Transparency

Users should always understand:

- which provider is being used
- which model is selected
- which files are modified
- which tools are executed
- which permissions are requested

The application should never hide important actions from users.

---

## Extensibility

Adding a provider should require implementing an adapter.

The application core should remain unchanged.

---

# 11. Product Scope

The initial product focuses on AI-assisted software development.

Core capabilities include:

- AI provider management
- workspace management
- conversations
- file modifications
- visual diffs
- MCP management
- Skill management
- model selection
- remote access
- permissions

---

# 12. Out of Scope

The following features are intentionally outside the initial scope.

- AI model training
- distributed inference
- cloud-hosted AI services
- collaborative real-time editing
- telemetry collection
- user tracking
- marketplace
- billing
- project hosting

These areas may be explored in independent products but are not objectives of AI Agent Hub.

---

# 13. User Experience Philosophy

The application should feel simple regardless of the underlying complexity.

Users should think in terms of:

- Workspaces
- Conversations
- Providers
- File Changes

They should not need to understand:

- CLI arguments
- configuration files
- provider internals
- implementation details

The UI should abstract complexity without hiding important information.

---

# 14. Server & Remote Station

The application always runs as a Server.

Users may access it:

- locally
- from another computer on the LAN
- through future remote access mechanisms

Remote Stations use exactly the same interface as local users.

No separate client application is required.

---

# 15. Security Model

Version 0.1 supports a single administrator account.

Authentication is mandatory.

Passwords are stored as secure password hashes.

Provider credentials are encrypted before storage.

All communication occurs through HTTPS.

Future versions may introduce:

- multiple users
- roles
- permissions
- MFA
- audit logs
- IP restrictions

---

# 16. Product Evolution

Version 0.1 focuses on delivering a complete single-user experience.

Version 0.2 expands the developer experience with Git integration, embedded editing, advanced previews and workflow improvements.

Future versions focus on collaboration, multi-user support and project synchronization.

---

# 17. Success Criteria

The project will be considered successful when a user can:

1. Install AI Agent Hub.
2. Detect installed AI providers automatically.
3. Configure providers without editing configuration files.
4. Open or create a Workspace.
5. Interact with multiple AI providers using the same workflow.
6. Review modifications through visual diffs.
7. Preview common file types without external applications.
8. Access the same environment locally or remotely.
9. Add new providers without modifying the application core.

---

# 18. Terminology

| Term | Definition |
|------|------------|
| **Server** | The application instance responsible for executing AI providers. |
| **Remote Station** | A browser connected to a Server. |
| **Workspace** | A managed software project. |
| **Conversation** | A persistent interaction with an AI provider. |
| **Provider** | An implementation capable of communicating with an AI system. |
| **Skill** | A provider-specific reusable capability. |
| **MCP** | A Model Context Protocol server/tool available to a provider. |

---

# 19. Design Philosophy

AI Agent Hub should provide a professional, predictable and transparent experience.

Every architectural decision should favor:

- simplicity over cleverness
- extensibility over shortcuts
- consistency over provider-specific behavior
- transparency over automation
- security over convenience

If a future feature conflicts with these principles, the principles take precedence.