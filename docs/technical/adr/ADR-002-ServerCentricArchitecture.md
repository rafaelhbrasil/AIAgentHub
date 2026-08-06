# ADR-002

# Server-Centric Architecture

**Status:** Accepted

---

# Context

AI providers require access to:

- Workspaces
- Conversations
- Credentials
- MCPs
- Skills

Distributing business logic between multiple clients would complicate synchronization and security.

---

# Decision

All business logic shall execute on the Server.

The Server owns:

- Workspaces
- Providers
- Conversations
- Authentication
- Permissions
- Git
- MCPs
- Skills

Remote Stations remain lightweight clients responsible only for presentation.

---

# Consequences

## Positive

- Single source of truth.
- Simpler synchronization.
- Better security.
- Easier administration.
- Easier backup.
- Easier multi-user support.

## Negative

- Server becomes a critical component.
- Offline Remote Stations are not supported.

---

# Alternatives Considered

## Peer-to-peer execution

Rejected.

Introduces unnecessary complexity.

---

## Client-side provider execution

Rejected.

Requires provider installation on every machine and duplicates configuration.

---

# References

- Product.md
- Architecture.md