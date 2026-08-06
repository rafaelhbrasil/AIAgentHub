# AI Agent Hub

AI Agent Hub is a provider-agnostic platform for AI coding assistants.

Instead of replacing existing AI providers, AI Agent Hub discovers, installs, configures and orchestrates them through a unified interface, allowing developers to work with multiple providers using a consistent workflow.

The project is designed for local and self-hosted environments, providing a secure, extensible and maintainable foundation for AI-assisted software development.

---

# Goals

* Provide a single interface for multiple AI coding providers.
* Support provider-specific capabilities without reducing them to a lowest common denominator.
* Enable secure local and remote access through a web interface.
* Keep the architecture provider-agnostic and extensible.
* Build a long-term maintainable platform rather than a provider-specific client.

---

# Planned Provider Support

Examples of supported providers include:

* OpenAI Codex CLI
* Gemini CLI
* Claude Code
* OpenCode

Additional providers may be added in future releases.

---

# Planned Features

## Version 0.1 (MVP)

* Provider discovery
* Guided provider installation
* Guided authentication
* Workspace management
* Persistent conversations
* AI-assisted code editing
* Side-by-side and unified file diffs
* File preview (Markdown, images and text files)
* MCP support
* Skills support
* Remote browser access
* HTTPS support
* Single-user authentication

## Future Versions

* Multi-user support
* Workspace synchronization
* Git integration
* Plugin system
* Provider SDK
* Mobile-friendly interface
* Advanced administration

See the documentation for the complete roadmap.

---

# Architecture

AI Agent Hub follows a layered architecture based on:

* Domain-Driven Design (DDD)
* Clean Architecture
* API-First design
* Server-centric execution
* Provider Adapter Pattern

Business logic always executes on the Server.

Both local and remote users interact with the same Web UI through the public REST and SignalR (WebSocket) APIs.

---

# Repository Structure

```text
/
├── docs/
├── src/
├── tests/
├── plugins/
├── tools/
├── samples/
├── .github/
├── LICENSE
├── NOTICE
├── README.md
└── AIAgentHub.sln
```

## Repository Overview

### docs/

Project documentation.

Includes product documentation, technical documentation, architecture decision records (ADRs), AI assistant guidance and diagrams.

---

### src/

Production source code.

Contains all application projects and libraries.

---

### tests/

Automated tests.

Includes unit tests, integration tests and future end-to-end tests.

---

### plugins/

Reserved for future plugin development.

---

### tools/

Development utilities, helper scripts and maintenance tools.

---

### samples/

Sample projects, example configurations and demonstration assets.

---

### .github/

GitHub workflows, issue templates and pull request templates.

---

# Documentation

Project documentation is organized into four major areas.

```text
docs/

├── product/
│   ├── Product.md
│   ├── Glossary.md
│   ├── Roadmap.md
│   ├── Changelog.md
│   └── releases/
│       ├── Release-v0.1.md
│       └── Release-v0.2.md
│
├── technical/
│   ├── Architecture.md
│   ├── DomainModel.md
│   ├── ApiDesign.md
│   ├── SecurityArchitecture.md
│   ├── DevelopmentStandards.md
│   ├── RepositoryStructure.md
│   ├── ContributingGuide.md
│   └── adr/
│       ├── ADR-001-ApiFirst.md
│       ├── ADR-002-ServerCentricArchitecture.md
│       ├── ADR-003-WebUiAsPrimaryClient.md
│       ├── ADR-004-ProviderAdapterPattern.md
│       ├── ADR-005-SingleExecutableArchitecture.md
│       ├── ADR-006-WorkspaceAbstraction.md
│       ├── ADR-007-AuthenticationModel.md
│       ├── ADR-008-SecurityModel.md
│       ├── ADR-009-PersistenceStrategy.md
│       ├── ADR-010-SignalRRealtimeArchitecture.md
│       └── ADR-011-CryptographyAndSecrets.md
│
├── assistant/
│   ├── Context.md
│   └── Workflow.md
│
├── reviews/        # implementation reviews
│
└── diagrams/
```

Refer to the documentation inside each folder for detailed information.

---

# Getting Started

## Requirements

* .NET 10 SDK
* Git
* One or more supported AI provider CLIs

Additional provider-specific requirements are documented separately.

---

## Build

```bash
git clone https://github.com/<your-account>/AIAgentHub.git

cd AIAgentHub

dotnet restore

dotnet build
```

---

## Run

```bash
dotnet run --project src/AIAgentHub.Web
```

The application starts a local web server.

By default, it is intended to be accessed through a web browser.

---

# Development

Before contributing, please read:

* Product documentation
* Technical documentation
* Architecture Decision Records (ADRs)

Development standards and project conventions are documented under:

```text
docs/technical/
```

Guidance for AI assistants is available under:

```text
docs/assistant/
```

---

# Contributing

Contributions are welcome.

Before opening a Pull Request:

* Ensure the solution builds successfully.
* Ensure tests pass.
* Follow the project's development standards.
* Update documentation when required.
* Respect accepted Architecture Decision Records (ADRs).

---

# Roadmap

The current roadmap is maintained in:

```text
docs/product/Roadmap.md
```

---

# License

Copyright © 2026 Rafael Brasil.

Licensed under the Apache License, Version 2.0.

See the `LICENSE` file for details.
