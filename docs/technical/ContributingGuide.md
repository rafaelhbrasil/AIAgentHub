# AI Agent Hub

# Contributing Guide

**Version:** 0.1 Draft

---

# Welcome

Thank you for your interest in contributing to AI Agent Hub.

Whether you are fixing a typo, improving documentation, implementing a new feature or reporting a bug, every contribution is appreciated.

This document explains how contributions should be prepared to keep the project consistent, maintainable and easy to review.

---

# Code of Conduct

Contributors are expected to:

- be respectful
- be constructive
- provide technical arguments
- accept feedback professionally

Disagreements should focus on ideas rather than individuals.

---

# Before Contributing

Before implementing a feature, please read:

- Product.md
- Architecture.md
- DevelopmentStandards.md
- Glossary.md

These documents define the project's direction and terminology.

---

# Reporting Issues

When opening an issue, include:

- clear description
- reproduction steps
- expected behavior
- actual behavior
- environment information
- screenshots when appropriate

One issue should describe one problem.

---

# Feature Requests

Feature requests should explain:

- the problem being solved
- the proposed solution
- possible alternatives
- expected benefits

Implementation details are optional.

---

# Architecture Changes

Architectural changes should not be implemented without documentation.

If a contribution changes the architecture:

- create a new ADR
- explain the decision
- describe alternatives considered
- document consequences

Existing ADRs should not be rewritten.

---

# Branch Strategy

Recommended branch names:

```
feature/<name>

bugfix/<name>

hotfix/<name>

release/<version>

docs/<topic>

refactor/<name>
```

Examples:

```
feature/provider-health-check

bugfix/workspace-loading

docs/update-roadmap
```

---

# Commit Messages

Commit messages should be clear and descriptive, explaining what was changed and why.

Guidelines:

- Summarize the main change clearly in the first line.
- Provide additional context or rationale in the commit description if the change is non-trivial.
- Keep messages informative so contributors can easily understand the project history.

Examples:

```text
Add provider capability detection for Antigravity CLI
Fix workspace loading issue when switching projects
Update product documentation for version 0.1 release
```

---

# Pull Requests

Each Pull Request should:

- compile successfully
- pass all tests
- update documentation when necessary
- remain focused on a single topic

Avoid unrelated changes.

Large Pull Requests should be split whenever practical.

---

# Documentation

Documentation is considered part of the implementation.

Whenever functionality changes, update the appropriate documents.

Examples:

Product changes:

```
Product/
```

Architecture changes:

```
Technical/
```

Release changes:

```
Product/Releases/
```

---

# Testing

Every feature should include appropriate automated tests.

At minimum:

- Unit Tests

When applicable:

- Integration Tests

Avoid reducing existing test coverage.

---

# Code Style

Follow the rules defined in:

```
DevelopmentStandards.md
```

Avoid introducing inconsistent naming or architecture.

---

# Naming

Always use terminology defined in:

```
Glossary.md
```

Examples:

✔ Workspace

✖ Project

✔ Provider

✖ Engine

✔ Remote Station

✖ Client

---

# Dependencies

Before introducing a new dependency, consider:

- maintenance
- licensing
- community support
- security
- package size

Avoid unnecessary dependencies.

---

# Performance

Optimize only after measuring.

Avoid premature optimization.

Maintainability has priority over micro-optimizations.

---

# Security

Security-related changes require additional review.

Examples include:

- authentication
- authorization
- cryptography
- networking

Whenever applicable, update:

```
SecurityArchitecture.md
```

---

# Review Checklist

Before requesting review, verify:

- builds successfully
- tests pass
- documentation updated
- naming follows Glossary
- architecture remains consistent
- no warnings introduced

---

# AI-Assisted Contributions

AI-assisted development is welcome.

However, contributors remain responsible for:

- correctness
- security
- maintainability
- documentation

Generated code should be reviewed before submission.

---

# Questions

If unsure about an implementation, prefer opening a discussion before writing code.

Small discussions often prevent large refactorings later.

---

# Thank You

Every contribution helps improve AI Agent Hub.

Thank you for helping build a provider-agnostic platform for AI coding agents.