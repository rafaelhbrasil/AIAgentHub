# AI Agent Hub

# Architecture

**Version:** 0.1 Draft

---

# Purpose

This document describes the high-level architecture of AI Agent Hub.

It explains how the major components interact and establishes the architectural principles that guide future development.

Implementation details such as frameworks, libraries and coding standards are documented separately in **TechnicalRequirements.md**.

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
                  HTTPS / WebSocket
                         |
+------------------------------------------------------+
|                    AI Agent Hub                      |
|------------------------------------------------------|
|                    Web UI                            |
|------------------------------------------------------|
|                 REST / WebSocket API                 |
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
- WebSocket API

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
- provider execution

Infrastructure depends on the Domain.

Never the opposite.

---

## Provider Layer

Each AI provider is implemented as an adapter.

Examples:

- CodexProvider
- GeminiProvider
- ClaudeProvider
- OpenCodeProvider

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

REST API

↓

Application Service

↓

Domain

↓

Provider

↓

AI CLI

↓

Response

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

- TechnicalRequirements.md
- ADRs