# AI Agent Hub

# Changelog

All notable changes to this project will be documented in this file.

The format is based on **Keep a Changelog** and follows **Semantic Versioning**.

## Versioning

The project follows Semantic Versioning:

```
MAJOR.MINOR.PATCH
```

Examples:

- 0.1.0
- 0.1.1
- 0.2.0
- 1.0.0

---

# Categories

Changes should be grouped using the following categories whenever applicable.

## Added

New functionality.

## Changed

Changes to existing functionality.

## Deprecated

Features scheduled for removal.

## Removed

Removed functionality.

## Fixed

Bug fixes.

## Security

Security-related improvements.

---

# [Unreleased]

## Added

- Initial documentation structure.
- Product documentation.
- Technical documentation.
- Release planning.
- Architecture Decision Records (ADR) structure.

---

# [0.1.0] - Initial MVP

**Release Date:** TBD

The first public release establishes the architectural foundation of AI Agent Hub.

The primary objective of this release is to provide a complete single-user experience capable of orchestrating multiple AI coding agents through a unified, provider-agnostic interface.

For the complete functional specification, see:

- `/docs/Product/Releases/Release-v0.1.md`

## Added

### Product

- Initial AI Agent Hub MVP.
- Provider-agnostic architecture.
- Server-centric execution model.
- Browser-based local and remote interface.

### Provider Management

- Automatic provider detection.
- Guided provider installation.
- Guided authentication.
- Model discovery.
- Model selection.
- Provider capability detection.

### Workspace Management

- Workspace creation.
- Workspace opening.
- Workspace explorer.
- Persistent Workspace settings.
- Global application settings.

### Conversations

- Persistent conversations.
- Conversation history.
- Conversation rename.
- Conversation deletion.
- Conversation search.
- Markdown rendering.
- Syntax highlighting.
- Streaming responses.
- Code block rendering.

### AI Execution

- Agentic file access.
- Permission requests.
- Execution log.
- MCP support.
- Skills support.
- Provider-specific capabilities.

### File Management

- File change detection.
- Side-by-side diff viewer.
- Unified diff viewer.
- Accept changes.
- Reject changes.
- File change history.

### File Preview

Support for previewing:

- Markdown
- Images
- Plain text
- JSON
- XML
- YAML

### Server

- HTTPS support.
- Configurable listening port.
- Localhost mode.
- LAN mode.
- Selected network interface mode.
- Single administrator account.
- Secure session management.
- Encrypted provider credentials.

### Remote Station

- Browser-based access.
- Shared Workspaces.
- Shared conversations.
- Shared AI execution.
- File preview.
- Diff review.

### Security

- Password hashing.
- Secret encryption.
- HTTPS communication.
- Permission validation.
- Secure authentication.

### Documentation

Initial project documentation including:

- Product documentation.
- Roadmap.
- Release documentation.
- Architecture.
- Technical standards.
- Repository structure.
- Security architecture.
- API reference.
- Glossary.
- Contribution guidelines.
- Architecture Decision Records.

## Security

Initial security architecture introduced.

Highlights include:

- Mandatory authentication.
- Password hashing.
- Encrypted provider secrets.
- HTTPS communication.
- Permission-based AI operations.

---

# Migration Notes

## 0.1.0

Initial public release.

No migration is required.

---

# References

Product vision:

- `/docs/Product/Product.md`

Roadmap:

- `/docs/Product/Roadmap.md`

Release specifications:

- `/docs/Product/Releases/`

Architecture:

- `/docs/Technical/Architecture.md`

Architecture Decision Records:

- `/docs/Technical/ADR/`