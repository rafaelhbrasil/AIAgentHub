# AI Agent Hub

# Repository Structure

**Version:** 0.1 Draft

---

# Purpose

This document describes the organization of the AI Agent Hub repository.

Its purpose is to ensure a predictable project structure that remains easy to navigate as the codebase grows.

Every contributor should follow these conventions.

---

# Repository Layout

```
/
├── docs/
├── src/
├── tests/
├── tools/
├── samples/
├── .github/
├── .editorconfig
├── .gitignore
├── LICENSE
├── README.md
└── AIAgentHub.sln
```

---

# Root

The repository root should remain clean.

Only files that are relevant to the entire solution belong here.

Examples include:

- solution file
- README
- license
- editor configuration
- global configuration

Avoid placing implementation files in the repository root.

---

# docs/

Contains all project documentation.

```
docs/

Product/
Technical/
Diagrams/
Assistant/
```

Documentation should evolve together with the project.

---

# src/

Contains every production project.

Each project should have a single responsibility.

Initial structure:

```
src/

AIAgentHub.Domain

AIAgentHub.Application

AIAgentHub.Infrastructure

AIAgentHub.Web
```

Future projects may include:

```
AIAgentHub.Plugins

AIAgentHub.Git

AIAgentHub.Provider.SDK

AIAgentHub.Migrations
```

Projects should remain independent whenever possible.

---

# tests/

Contains all automated tests.

```
tests/

AIAgentHub.Domain.Tests

AIAgentHub.Application.Tests

AIAgentHub.Infrastructure.Tests

AIAgentHub.Web.Tests

AIAgentHub.Integration.Tests
```

Tests should mirror the production structure.

---

# tools/

Contains helper tools used during development.

Examples:

- scripts
- local utilities
- code generation
- migration helpers

Tools are not part of the application.

---

# samples/

Contains sample content.

Examples:

- sample Workspaces
- sample providers
- configuration examples
- test assets

Sample code should never be referenced by production code.

---

# .github/

Contains GitHub-specific resources.

Examples:

- workflows
- issue templates
- pull request templates
- discussion templates

---

# Documentation Organization

Documentation is divided into four areas.

```
docs/

Product/

Technical/

Assistant/

Diagrams/
```

Each area serves a distinct purpose.

---

# Product

Contains product documentation.

Examples:

- Product
- Roadmap
- Releases
- Changelog

---

# Technical

Contains engineering documentation.

Examples:

- Architecture
- Development Standards
- Security
- API
- ADR

---

# Assistant

Contains documentation intended primarily for AI assistants.

Examples:

- project context
- coding instructions
- implementation guidelines
- prompt templates

These documents complement, but never replace, the official technical documentation.

---

# Diagrams

Contains visual documentation.

Preferred formats:

- Draw.io
- Mermaid
- PNG exports

Generated images should never become the primary documentation source.

The corresponding editable diagram must always be preserved.

---

# Source Code Organization

Namespaces should mirror folder structure whenever practical.

Example:

```
AIAgentHub.Application.Workspaces

↓

src/

AIAgentHub.Application/

Workspaces/
```

---

# Folder Organization

Folders should be organized by feature before technology whenever practical.

Preferred:

```
Workspaces/

Conversations/

Providers/
```

Avoid folders such as:

```
Helpers/

Misc/

Common/

Utilities/
```

unless a clear architectural reason exists.

---

# Resources

Static resources should remain organized.

Example:

```
Images/

Icons/

Fonts/

Localization/
```

Generated resources should be separated from manually maintained resources.

---

# Configuration Files

Configuration belongs in dedicated configuration files.

Avoid hardcoded values.

Environment-specific configuration should remain isolated.

---

# Generated Files

Generated files should be clearly separated.

Whenever possible they should not be committed unless required.

Examples include:

- generated code
- temporary assets
- caches

---

# Third-Party Code

Third-party source code should never be mixed with project source code.

External dependencies should be referenced through package managers whenever possible.

---

# Naming Conventions

Project names should follow:

```
AIAgentHub.<Area>
```

Examples:

```
AIAgentHub.Domain

AIAgentHub.Application

AIAgentHub.Infrastructure

AIAgentHub.Web
```

Test projects:

```
AIAgentHub.Domain.Tests

AIAgentHub.Application.Tests
```

---

# File Organization

One public type per file.

File names should match the primary type.

Avoid multiple unrelated classes in the same file.

---

# Future Growth

The repository structure should remain stable as new functionality is added.

Future additions should integrate naturally into the existing organization rather than introducing parallel structures.

When significant structural changes become necessary, they should be documented through an ADR before implementation.