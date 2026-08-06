# ADR-005

# Single Executable Architecture

**Status:** Accepted

**Date:** 2026-08-06

---

# Context

AI Agent Hub is intended to be easy to install and maintain.

Requiring multiple applications (Server, Remote Station, background services, configuration tools, etc.) would increase installation complexity, updates and support burden.

The project should remain simple for individual developers while still supporting future expansion.

---

# Decision

AI Agent Hub shall be distributed as a single executable application.

The executable hosts:

- Web UI
- REST API
- WebSocket API
- Application Services
- Background Services

No additional executables are required for normal operation.

Future optional extensions (plugins, SDKs, CLI tools) must remain independent from the core executable.

---

# Consequences

## Positive

- Extremely simple installation.
- Easier updates.
- Easier distribution.
- Lower maintenance cost.
- Consistent execution environment.
- Better user experience.

## Negative

- The executable becomes larger.
- Startup responsibilities increase.
- Some optional features may require lazy loading.

---

# Alternatives Considered

## Separate Server and UI executables

Rejected.

Would complicate deployment without providing significant benefits.

---

## Microservices

Rejected.

Adds unnecessary complexity for the project's intended deployment model.

---

# References

- Product.md
- Architecture.md