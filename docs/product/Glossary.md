# AI Agent Hub

# Glossary

**Version:** 0.1 Draft

---

# Purpose

This document defines the official terminology used throughout AI Agent Hub.

The terms described here should be used consistently across:

- documentation
- source code
- user interface
- REST API
- WebSocket API (SignalR)
- Architecture Decision Records (ADRs)

If multiple names could describe the same concept, the term defined in this document takes precedence.

---

# Core Concepts

## Server

The AI Agent Hub instance responsible for executing AI providers and managing all application resources.

The Server owns:

- Workspaces
- Conversations
- Providers
- Configuration
- Authentication
- Permissions
- MCPs
- Skills

A Server may be accessed locally or remotely.

---

## Remote Station

A browser connected to a Server.

A Remote Station never executes AI providers locally.

Its purpose is to provide a user interface for interacting with the Server.

---

## Workspace

A logical software project managed by the Server.

A Workspace typically represents a folder containing source code and related files.

Each Workspace may contain:

- Conversations
- Provider configuration
- Workspace settings
- File history

Future versions may support additional Workspace origins.

---

## Conversation

A persistent interaction between a user and an AI provider.

Conversations belong to a single Workspace.

A Conversation stores:

- prompts
- responses
- execution history
- associated file changes

---

## Provider

An integration capable of communicating with an AI system.

Examples include:

- Antigravity CLI (`agy`) — Google DeepMind
- Gemini CLI
- Codex CLI
- Claude Code
- OpenCode

Providers are accessed through a common abstraction layer.

---

## Model

A specific AI model exposed by a Provider.

Examples include:

- GPT models
- Gemini models
- Claude models

A Provider may expose multiple Models.

---

## Tool

A capability that allows an AI provider to perform an action beyond text generation.

Examples include:

- reading files
- writing files
- executing commands

Tools are typically provided by the AI provider.

---

## MCP

A Model Context Protocol server or service available to a Provider.

MCPs extend the capabilities available to AI providers.

---

## Skill

A reusable provider capability.

The exact meaning depends on the Provider.

Whenever supported, AI Agent Hub exposes Skills without modifying their native behavior.

---

## Adapter

A software component responsible for integrating a Provider with AI Agent Hub.

Adapters isolate provider-specific implementation details.

---

## Diff

A visual representation of file modifications.

Supported modes include:

- Side-by-side
- Unified

---

## Preview

A read-only visualization of a file inside AI Agent Hub.

Preview is intended for inspection rather than editing.

---

## Dashboard

The application's main landing page.

The Dashboard provides quick access to:

- Workspaces
- Conversations
- Providers
- Status information

---

# Security

## Authentication

The process of verifying a user's identity.

Version 0.1 supports a single administrator account.

---

## Authorization

The process of determining whether an authenticated user is permitted to perform an action.

Authorization occurs exclusively on the Server.

---

## Permission

A specific authorization allowing an operation.

Examples include:

- editing files
- executing commands
- managing providers

---

## Session

A temporary authenticated connection between a user and the Server.

Sessions may expire or be revoked.

---

## Secret

Sensitive information requiring protection.

Examples include:

- API Keys
- OAuth tokens
- provider credentials

Secrets are encrypted before storage.

---

# Workspace Sharing

## Snapshot

A Workspace synchronization mode in which files are transferred only when explicitly requested.

---

## Synchronization

A Workspace synchronization mode where changes are exchanged automatically between the Server and a Remote Station.

---

## Workspace Origin

The location where a Workspace was initially created.

Possible origins include:

- Server
- Remote Station

Future versions may introduce additional origins.

---

# Architecture

## API First

An architectural principle stating that every application feature must be exposed through the public API.

The local interface and Remote Stations consume the same API.

---

## Server-Centric

An architectural principle stating that business logic executes exclusively on the Server.

Remote Stations remain lightweight clients.

---

## Provider Agnostic

An architectural principle stating that business logic should not depend on a specific AI provider.

---

## Adapter Pattern

The design pattern used to integrate Providers while isolating provider-specific implementation details.

---

## SignalR

The real-time communication framework used for streaming, push events and progress between the Server and clients.

SignalR builds on WebSocket where available and falls back to other transports automatically.

---

# User Interface

## Local Access

Accessing the Server through the same machine where it is running.

---

## Remote Access

Accessing the Server from another device.

---

## Localhost Mode

The Server listens only on the local machine.

---

## LAN Mode

The Server listens on all available network interfaces.

---

## Selected Interface Mode

The Server listens only on explicitly selected network interfaces.

---

# Repository

## Product Documentation

Documentation describing the product vision, roadmap and release planning.

---

## Technical Documentation

Documentation describing architecture, engineering standards and implementation details.

---

## ADR

Architecture Decision Record.

A document describing an architectural decision, its context, consequences and alternatives considered.

---

# Naming Rules

The following terms should be used consistently throughout the project.

| Preferred | Avoid |
|-----------|-------|
| Workspace | Project, Solution |
| Provider | Engine, Backend |
| Remote Station | Client |
| Conversation | Chat, Session |
| Model | AI, Engine |
| Server | Host |
| Tool | Function |
| Preview | Viewer |
| Diff | Compare |
| Secret | Credential (generic) |

---

# Future Terms

As new concepts are introduced, they should first be added to this document before appearing in other project documentation.

This glossary serves as the canonical reference for terminology across the AI Agent Hub project.