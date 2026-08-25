# AI Agent Hub

# Development Standards

**Version:** 0.1 Draft

---

# Purpose

This document defines the engineering standards used throughout AI Agent Hub.

Its purpose is to ensure consistency, maintainability, quality and long-term sustainability of the project.

Every contributor is expected to follow these standards.

---

# General Principles

The project values:

- Simplicity
- Readability
- Maintainability
- Extensibility
- Testability
- Security

Readable code is preferred over clever code.

Explicit code is preferred over implicit behavior.

Maintainability is preferred over premature optimization.

---

# Technology Stack

Current technology choices:

Backend
- .NET 10
- ASP.NET Core
- C#
- Entity Framework Core
- SQLite
- SignalR (real-time: streaming, events, progress) — see ADR-010

Frontend
- React
- TypeScript
- Vite

Technology decisions are documented separately through ADRs.

---

# Project Architecture

The project follows:

- Clean Architecture
- Domain Driven Design (DDD)
- API First
- Server-Centric Architecture

See:

- Architecture.md

---

# Coding Style

## Language

Use modern C# features whenever they improve readability.

Avoid obsolete APIs.

---

## Naming

Use descriptive names.

Avoid abbreviations unless universally recognized.

Examples:

Good:

- WorkspaceService
- ProviderManager
- ConversationRepository

Bad:

- WSMgr
- Util
- Misc

Always use the terminology defined in Glossary.md.

---

## Classes

A class should have one clear responsibility.

Large classes should be decomposed.

---

## Methods

Methods should:

- perform one task
- remain short
- avoid excessive nesting

Guard clauses are preferred over deeply nested conditions.

---

## Comments

Code should explain **why**, not **what**.

Avoid redundant comments.

Bad:

```csharp
// Increment i
i++;
```

Good:

```csharp
// Retry because the provider may still be starting.
```

---

# Nullable Reference Types

Nullable reference types must remain enabled.

Warnings should not be ignored.

---

# Warnings

Compiler warnings should be treated as errors.

The project should compile warning-free.

---

# Dependency Injection

Constructor injection is the preferred approach.

Avoid:

- service locators
- static service access

---

# Asynchronous Programming

Use async/await whenever appropriate.

Avoid synchronous blocking on asynchronous operations.

Examples to avoid:

- Task.Wait()
- Result

---

# Exceptions

Exceptions represent exceptional situations.

Do not use exceptions for ordinary control flow.

---

# Logging

Log meaningful events.

Avoid excessive logging.

Sensitive information must never be logged.

---

# Configuration

Configuration should be strongly typed.

Magic strings should be avoided.

---

# Persistence

Business logic must not depend on Entity Framework.

Repositories expose abstractions.

Infrastructure implements persistence.

---

# Testing

Every new feature should include tests.

Testing pyramid:

- Unit Tests
- Integration Tests

Avoid UI tests unless necessary.

---

# Unit Tests

Unit tests should:

- be deterministic
- run quickly
- avoid external dependencies

---

# Integration Tests

Integration tests validate interactions between components.

They should execute against isolated environments.

---

# Code Reviews

Every Pull Request should verify:

- correctness
- readability
- architecture
- security
- tests

---

# Documentation

Every significant feature should update:

- Product documentation
- Release documentation
- ADRs (if architecture changes)

Documentation is part of the implementation.

---

# Git

Recommended branch naming:

feature/

bugfix/

hotfix/

release/

---

# Commit Messages

Commit messages should be clear and descriptive, explaining what was changed and why.

Guidelines:

- State the purpose of the change clearly in the first line.
- Provide additional context or reasoning in the description when helpful.
- Avoid vague messages like `update`, `fix`, `wip`, or `changes`.

Examples:

```text
Add provider capability detection for Antigravity CLI
Resolve workspace synchronization bug when switching projects
Update product documentation for version 0.1 release
```

---

# Pull Requests

A Pull Request should:

- compile
- pass all tests
- update documentation
- contain focused changes

Avoid mixing unrelated work.

---

# Performance

Optimize only after measurement.

Correctness comes before optimization.

Maintainability comes before micro-optimizations.

---

# Security

Security is everyone's responsibility.

Every change should consider:

- authentication
- authorization
- secret management
- logging
- input validation

---

# Backward Compatibility

Breaking changes should be avoided.

When unavoidable:

- document them
- justify them
- include migration guidance

---

# Continuous Improvement

These standards evolve with the project.

When improvements are identified, prefer updating this document rather than relying on tribal knowledge.