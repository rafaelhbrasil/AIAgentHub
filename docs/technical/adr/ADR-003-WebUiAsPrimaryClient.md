# ADR-003

# Web UI as the Primary Client

**Status:** Accepted

---

# Context

The project initially considered creating two desktop applications:

- Server
- Remote Station

Maintaining two applications would duplicate user interface development and deployment.

---

# Decision

AI Agent Hub shall expose a Web UI hosted by the Server.

Both local and remote users access the same interface.

The local experience may use an embedded browser (WebView), but this remains the same Web application.

---

# Consequences

## Positive

- Single UI.
- Easier maintenance.
- Platform independence.
- Remote access requires no installation.
- Mobile compatibility becomes possible.
- Native clients may be added later without changing the backend.

## Negative

- Requires a browser engine.
- Desktop-specific integrations require additional work.

---

# Alternatives Considered

## Separate desktop client

Rejected.

Would duplicate UI development.

---

## Electron application

Rejected for the initial release.

May be reconsidered in the future if native packaging provides significant value.

---

# References

- Architecture.md
- APIContract.md