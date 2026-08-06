# ADR-010

# SignalR for Real-Time Communication

**Status:** Accepted

**Date:** 2026-08-06

---

# Context

AI Agent Hub relies on real-time server-to-client communication for several core capabilities:

- streaming AI responses
- conversation lifecycle events (`conversation.started`, `.updated`, `.completed`)
- progress updates for long-running operations
- provider status changes
- Workspace change notifications
- file-change notifications (`diff.created`)
- permission requests
- notifications

The project had only a generic "WebSocket" description (REST for request/response, WebSocket for real-time). This left the transport and client/server integration model unspecified.

The backend is ASP.NET Core (.NET 10), which provides SignalR as a real-time framework built on top of WebSocket (with automatic fallback transports). The frontend is React + TypeScript + Vite.

---

# Decision

AI Agent Hub shall use **SignalR** for all real-time client-server communication described above.

The transport details (WebSocket vs Server-Sent Events vs long polling) are abstracted by SignalR; consumers interact through typed hub methods and strongly-typed messages rather than raw frames.

REST continues to handle request/response operations. SignalR handles streaming and push events. Connection management, reconnection and client-visible state are owned by the SignalR hub-layer, not by the Web UI or Remote Station.

---

# Advantages of SignalR

- **Abstraction over transport.** Code targets hubs, not raw sockets; the same app logic works over WebSocket with automatic fallback (Server-Sent Events, Long Polling) through company-environment proxies without application changes.
- **Faster development.** Grouping, connection ID, typed clients (`IHubContext`) and client-server method invocation are built in — no custom message framing/parsing code.
- **Automatic reconnection.** SignalR restores state after network drops; essential for long-running streaming and agent execution.
- **Streaming support.** Built-in streaming (stream responses as they arrive) maps directly to AI response streaming.
- **Scaling and groups.** SignalR scalable/backplane support leaves room for future multi-user/Server groups (v0.2/v0.3+) without redesign.
- **Strong typing.** Typed hub interfaces reduce both client and server errors and are easy to contract-test.
- **Security integration.** Integrated with ASP.NET Core authentication/authorization; each streaming or permission invocation can be authorized.
- **Single library alignment.** First-party within ASP.NET Core — no new external dependency, matches the .NET stack already chosen (DevelopmentStandards / ADR-009).

---

# Consequences

## Positive

- One real-time mechanism instead of hand-rolled WebSocket plumbing.
- Faster, safer streaming and permission-push flows.
- Reconnection semantics reduce perceived failures for remote users.
- Future multi-user/real-time collaboration (ADR-006, v0.3) becomes easier to add.

## Negative

- Slight abstraction between the app and raw WebSocket; tests target hubs rather than raw sockets.
- Clients must use SignalR's JavaScript/React client library (a small additional bundle).
- SignalR-specific terminology must be used in API docs (hubs vs e.g. socket routes).

---

# Alternatives Considered

## Raw WebSocket

Rejected for the default path.

Offers maximum control but requires manual connection lifecycle, fallback, reconnection, framing, and scaling — significant extra effort for no immediate benefit. SignalR provides these built on the same WebSocket transport when available.

## SSE (Server-Sent Events) only

Rejected.

One-way only (client→server not supported); insufficient for the bidirectional AI execution driver.

## gRPC-streaming only

Rejected.

Requires browser ecosystem (gRPC-web) and adds complexity without being the same abstraction SignalR provides by default.

---

# Implementation Notes

- SignalR is exposed (both local Web admin / Remote Stations) over the same HTTPS layer as the REST API.
- Endpoints:
  - REST for request/response (unchanged)
  - SignalR for real-time events, streaming, and progress
- Real-time contract details (hub methods, event names) are part of the run-time/OpenAPI-related real-time design (see `ApiDesign.md`).

---

# References

- Architecture.md
- ApiDesign.md
- DevelopmentStandards.md
- ADR-001 (API First)
- ADR-003 (Web UI as the Primary Client)