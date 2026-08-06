# ADR-006

# Workspace Abstraction

**Status:** Accepted

**Date:** 2026-08-06

---

# Context

Projects may originate from different locations.

Examples include:

- Local Server
- Remote Station
- Git Repository
- ZIP archive
- Future cloud storage

Business logic should not depend on where a project originated.

---

# Decision

The application shall work exclusively with the concept of a **Workspace**.

A Workspace represents a logical project regardless of its physical origin.

Future versions may support multiple Workspace origins without affecting the remainder of the application.

Examples:

- Server Workspace
- Remote Workspace
- Git Workspace
- Imported Workspace

Once opened, every Workspace behaves identically.

---

# Consequences

## Positive

- Storage-independent architecture.
- Easier future synchronization.
- Simpler business logic.
- Easier testing.
- Consistent user experience.

## Negative

- Additional abstraction layer.
- Workspace loading requires adapters for each origin.

---

# Alternatives Considered

## Using filesystem paths directly

Rejected.

Would tightly couple the application to local storage.

---

## Separate project types

Rejected.

Would unnecessarily complicate the user experience.

---

# References

- Product.md
- Architecture.md