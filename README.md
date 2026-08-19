# AI Agent Hub

AI Agent Hub is a provider-agnostic platform for AI coding assistants.

Instead of replacing existing AI providers, AI Agent Hub discovers, installs, configures and orchestrates them through a unified interface, allowing developers to work with multiple providers using a consistent workflow.

The project is designed for local and self-hosted environments, providing a secure, extensible and maintainable foundation for AI-assisted software development.

---

# License

AI Agent Hub is open-source software licensed under the Apache License 2.0.

The software is provided "AS IS" as described in the license.

Users are responsible for evaluating the risks of running AI coding agents with access to their systems and for complying with the terms of any third-party providers they use.

See the [LICENSE](LICENSE) file for details.

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
* Node.js 18+ & npm (for frontend development and asset builds)
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

### Option 1: Standard / Self-Contained Execution
The production React frontend bundle is pre-built into `src/AIAgentHub.Web/wwwroot`. You only need to run the .NET application:

```bash
dotnet run --project src/AIAgentHub.Web
```

Open **`https://localhost:5432`** in your browser.

### Option 2: Live Frontend Development (HMR)
If you are developing or modifying the React + TypeScript frontend and want instant hot-module reloading:

1. **Install frontend dependencies (first time only, from repository root):**
```bash
npm install
```

2. **Start the .NET backend:**
```bash
dotnet run --project src/AIAgentHub.Web
```

3. **Start the Vite development server (in a separate terminal):**
```bash
npm run dev
```

4. **Open the live application:**
Navigate to **`http://localhost:5173`** in your browser. The Vite dev server automatically proxies API (`/api`) and SignalR (`/hubs`) requests to `https://localhost:5432`.

### Frontend Scripts (npm Workspaces)
You can run all frontend scripts directly from the repository root:

- **`npm install`** — Installs all workspace dependencies (run once after cloning)
- **`npm run dev`** — Starts the Vite dev server with Hot Module Reloading
- **`npm run build`** — Compiles TypeScript and builds production assets into `src/AIAgentHub.Web/wwwroot/assets/`
- **`npm test`** — Runs frontend unit tests with Vitest

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
