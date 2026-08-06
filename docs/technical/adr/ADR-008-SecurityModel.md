# ADR-008

# Security Model

**Status:** Accepted

**Date:** 2026-08-06

---

# Context

AI Agent Hub manages sensitive information including:

- Provider credentials
- Source code
- Conversations
- Workspace configuration

Security must therefore be considered a core architectural concern rather than an implementation detail.

---

# Decision

The application adopts the following security principles:

- HTTPS by default.
- Mandatory authentication.
- Password hashing.
- Encrypted secret storage.
- Server-side authorization.
- Explicit permission requests for sensitive operations.
- Least privilege whenever applicable.

Sensitive information must never be stored in plain text.

The Server is solely responsible for enforcing security policies.

---

# Consequences

## Positive

- Strong security baseline.
- Consistent authorization model.
- Better protection of credentials.
- Easier future security enhancements.
- Clear separation of responsibilities.

## Negative

- Additional implementation effort.
- Certificate management required.
- Slightly more complex deployment.

---

# Alternatives Considered

## HTTP support

Rejected.

Authenticated traffic should always be protected.

---

## Plain-text secret storage

Rejected.

Provider credentials must always be encrypted before persistence.

---

## Client-side authorization

Rejected.

Authorization must always be enforced by the Server.

---

# References

- SecurityArchitecture.md
- Architecture.md
- Product.md