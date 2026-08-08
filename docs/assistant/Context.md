# AI Agent Hub

# Context

**Version:** 0.1 Draft

---

# Purpose

This document provides a high-level overview of AI Agent Hub for AI assistants.

Its purpose is to quickly establish project context before implementation begins.

This document is **not** the source of truth for product requirements or architecture.

Instead, it directs assistants to the appropriate documentation.

---

# Project Summary

AI Agent Hub is a provider-agnostic platform for AI coding assistants.

It provides a unified interface capable of orchestrating multiple AI providers through a consistent user experience.

Supported providers may include, but are not limited to:

* Antigravity CLI (`agy`) — Google DeepMind
* OpenAI Codex CLI
* Gemini CLI
* Claude Code
* OpenCode

The application does not replace these providers.

Instead, it discovers, installs, configures and orchestrates them through a common interface.

---

# Vision

The long-term vision is to provide a single application capable of managing AI coding workflows independently of the underlying provider.

Users should be able to switch providers without learning a different interface for each one.

Whenever possible, provider-specific capabilities should remain available instead of being reduced to a lowest-common-denominator feature set.

---

# Product Principles

The project is guided by the following principles:

* Provider agnostic
* API First
* Server-centric
* Privacy first
* Extensible
* Secure by default
* Maintainable
* Self-hosted

---

# Current Scope

Version 0.1 focuses on delivering a complete single-user experience.

Major capabilities include:

* Provider discovery
* Guided provider installation
* Guided authentication
* Workspace management
* Persistent conversations
* AI-assisted code editing
* File diff visualization
* File preview
* MCP support
* Skills support
* Remote browser access
* Secure local deployment

Future releases expand these capabilities without changing the core architecture.

---

# Architecture Summary

The application follows a layered architecture.

```
Browser

↓

REST API / WebSocket (SignalR)

↓

Application

↓

Domain

↓

Infrastructure

↓

AI Providers
```

Business logic always executes on the Server.

The browser is responsible only for presentation.

---

# Repository Overview

The repository is organized into:

```
docs/
src/
tests/
plugins/
tools/
samples/
```

Detailed repository conventions are documented in:

```
Technical/RepositoryStructure.md
```

---

# Documentation Structure

Documentation is divided into four areas.

```
Product/
```

Product vision, roadmap and release planning.

```
Technical/
```

Architecture, engineering standards and implementation details.

```
Technical/ADR/
```

Architectural decisions.

```
Assistant/
```

Documentation intended specifically for AI assistants.

---

# Reading Order

Before implementing any change, review the following documents.

1. Product/Product.md
2. Product/Glossary.md
3. Technical/Architecture.md
4. Technical/DevelopmentStandards.md
5. Technical/SecurityArchitecture.md
6. Technical/RepositoryStructure.md
7. Relevant ADRs
8. Relevant Release document (if applicable)

If information conflicts, follow the precedence defined below.

---

# Documentation Priority

When multiple documents discuss the same topic, use the following priority.

1. Accepted ADRs
2. Architecture
3. Product documentation
4. Development Standards
5. Release documentation

This document is only a navigation guide and never overrides those documents.

---

# Terminology

Always use the terminology defined in:

```
Product/Glossary.md
```

Do not introduce alternative names for existing concepts.

Examples:

* Workspace
* Provider
* Conversation
* Remote Station
* Server

---

# Implementation Philosophy

When implementing features:

* Prefer small, focused changes.
* Preserve existing architecture.
* Follow established naming conventions.
* Keep business logic independent of providers.
* Favor maintainability over clever solutions.
* Avoid unnecessary dependencies.

When requirements are ambiguous, ask for clarification rather than making assumptions.

---

# Current Development Stage

The project is currently focused on Version 0.1 (MVP).

Primary goals are:

* Establish a solid architectural foundation.
* Deliver a polished single-user experience.
* Keep the codebase simple and extensible.
* Minimize technical debt.

Future versions should build upon this foundation rather than redesign it.

---

# References

Product documentation:

```
docs/Product/
```

Technical documentation:

```
docs/Technical/
```

Architecture decisions:

```
docs/Technical/ADR/
```

Assistant workflow:

```
docs/Assistant/Workflow.md
```
