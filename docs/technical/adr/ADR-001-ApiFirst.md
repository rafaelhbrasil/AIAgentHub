# ADR-001

# API First Architecture

**Status:** Accepted

---

# Context

AI Agent Hub supports both local and remote access.

Without a common communication layer, local users would execute business logic differently from remote users.

This would duplicate code paths, increase testing complexity and make future clients more difficult to develop.

---

# Decision

Every application feature shall be exposed through the public API.

The local Web UI must consume the exact same REST and WebSocket endpoints as every Remote Station.

No privileged internal execution path shall exist.

---

# Consequences

## Positive

- Single execution path.
- Easier automated testing.
- Easier future mobile applications.
- Easier native clients.
- Easier plugin integration.
- Consistent behavior regardless of client.

## Negative

- Slightly higher initial implementation effort.
- Every feature requires an API endpoint.
- Internal optimizations must respect API boundaries.

---

# Alternatives Considered

## Direct service calls from the local UI

Rejected.

Would create two execution paths.

---

## Separate API for remote clients

Rejected.

Would duplicate business logic and increase maintenance costs.

---

# References

- Architecture.md
- APIContract.md