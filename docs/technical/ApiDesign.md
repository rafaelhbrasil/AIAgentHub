# AI Agent Hub

# API Contract

**Version:** 0.1 Draft

---

# Purpose

This document defines the public API exposed by AI Agent Hub.

The API serves as the communication layer between:

- Local Web UI
- Remote Stations
- Future native clients
- Future mobile applications
- Third-party integrations

Every user interaction must ultimately be translated into one or more API calls.

No privileged internal endpoints exist.

REST endpoints are formally documented through OpenAPI.

Interactive documentation is available through Scalar during development.

The OpenAPI document is considered the authoritative source for:

- endpoints
- request schemas
- response schemas
- examples
- authentication requirements
- HTTP status codes

This document intentionally focuses on API design principles rather than duplicating generated contracts.

---

# Design Principles

The API follows these principles.

- REST for request/response operations.
- SignalR (real-time) for events and streaming.
- Stateless requests.
- HTTPS only.
- JSON payloads with all enums strictly serialized as strings (e.g. `ProviderStatus`, `MessageRole`, `DiffChangeType`, `NetworkMode`).
- Versioned endpoints.
- Consistent naming.
- Model identifiers accept `null` or `"default"` to indicate provider default delegation (the CLI omits `--model`); the built-in "default" model is never persisted to `ProviderModelSettings` table.

---

# Base URL & Client Routes

Examples:

```
https://localhost:5432
https://localhost:5432/workspaces/{workspaceId}/conversations/{conversationId}
https://localhost:5432/providers
```

SPA Client Routes:
- `/` or `/dashboard`: Dashboard View
- `/workspaces`: Workspaces List View
- `/workspaces/{workspaceId}`: Workspace Studio View
- `/workspaces/{workspaceId}/conversations/{conversationId}`: Direct Workspace Studio deep link to active conversation
- `/providers`: AI Providers View
- `/tools` / `/mcps`: Tools & Skills View
- `/settings`: Server Settings View

---

# API Versioning

All endpoints include an API version.

Example:

```
/api/v1/
```

Future versions may introduce:

```
/api/v2/
```

Version changes should avoid breaking existing clients whenever practical.

---

# Authentication

Authentication is required for every endpoint except those explicitly documented as anonymous.
Unauthenticated requests to API (`/api/*`) and real-time (`/hubs/*`) endpoints are rejected with `401 Unauthorized`. Direct browser requests to protected page routes redirect to `/`.

Future authentication mechanisms may include:

- session cookies
- bearer tokens
- API keys

Version 0.1 uses authenticated sessions.

---

# Authorization

Authorization is always enforced by the Server.

Clients must never assume authorization.

---

# Data Format

Request and response bodies use JSON.

Property names use camelCase.

Dates use ISO-8601.

Identifiers use UUIDs unless documented otherwise.

---

# Error Responses

Errors should follow a consistent structure.

Example:

```json
{
  "code": "workspace_not_found",
  "message": "Workspace does not exist."
}
```

---

# REST API

The REST API is organized by feature.

---

## Authentication & Setup

Examples:

```
GET /api/v1/auth/setup/status

POST /api/v1/auth/setup/initialize

POST /api/v1/auth/setup/reset

POST /api/v1/auth/login

POST /api/v1/auth/logout

GET /api/v1/auth/session

POST /api/v1/auth/recover
```

---

## Providers

Examples:

```
GET /api/v1/providers

GET /api/v1/providers/{id}

POST /api/v1/providers/install

POST /api/v1/providers/authenticate
```

---

## Models

Examples:

```
GET /api/v1/models

GET /api/v1/providers/{id}/models
```

---

## Workspaces

Examples:

```
GET /api/v1/workspaces
 
POST /api/v1/workspaces

GET /api/v1/workspaces/{id}

GET /api/v1/workspaces/{id}/download

DELETE /api/v1/workspaces/{id}
```

---

## Filesystem

Examples:

```
GET /api/v1/filesystem/drives

GET /api/v1/filesystem/browse?path={path}

GET /api/v1/filesystem/tree?workspaceId={workspaceId}

GET /api/v1/filesystem/forbidden-paths
```

---

## Conversations

Conversations are ordered by most recent user interaction (`lastUserInteractionAtUtc`) descending. Sending a prompt, creating a conversation, or renaming immediately updates this timestamp and moves the conversation to the top.

Examples:

```
GET /api/v1/conversations?workspaceId={workspaceId}

POST /api/v1/conversations

GET /api/v1/conversations/{id}

PATCH /api/v1/conversations/{id}

PUT /api/v1/conversations/{id}/model

DELETE /api/v1/conversations/{id}
```

---

## Providers

Examples:

```
GET /api/v1/providers

GET /api/v1/providers/{id}

GET /api/v1/providers/{id}/status?refresh=true

GET /api/v1/providers/{id}/models?refresh=true

PUT /api/v1/providers/{id}/models/settings

POST /api/v1/providers/{id}/authenticate
```

---

## AI Execution

Examples:

```
POST /api/v1/conversations/{id}/prompt

POST /api/v1/conversations/{id}/abort

POST /api/v1/execute
```

Realtime Stream & Events (via `/hubs/agent`):
- `streamChunk`: Delivers real-time response tokens.
- `conversation.started`: Emitted when prompt execution begins.
- `conversation.heartbeat`: Emitted periodically (default: 60s) during long-running execution turns with elapsed duration and progress description. Note: Heartbeat messages are ephemeral/client-only and never saved to the database.
- `conversation.completed`: Emitted on successful completion of a prompt turn.
- `conversation.aborted`: Emitted when execution is cancelled or times out.
- Automatic resume continuation turns are provider-only and not persisted as additional user messages.

---

## File Changes

Examples:

```
GET /api/v1/diffs

GET /api/v1/diffs/{id}

POST /api/v1/diffs/{id}/accept

POST /api/v1/diffs/{id}/reject

POST /api/v1/diffs/accept-all

POST /api/v1/diffs/reject-all
```

---

## Preview

Examples:

```
GET /api/v1/preview

GET /api/v1/files/{id}/preview
```

---

## MCP

Examples:

```
GET /api/v1/mcps

POST /api/v1/mcps/{id}/enable
```

---

## Skills

Examples:

```
GET /api/v1/skills

POST /api/v1/skills/{id}/enable
```

---

## Settings

Examples:

```
GET /api/v1/settings

PUT /api/v1/settings
```

---

## System

Examples:

```
GET /api/v1/system/version
```

The system version endpoint returns runtime assembly and build metadata:

- `version`: Assembly version (e.g. `0.1.0` in Release or `0.1.0.082602` in Debug)
- `informationalVersion`: Semantic version with optional build metadata (e.g. `0.1.0-debug+20260826022900`)
- `isDevelopment`: Boolean flag indicating whether the host is running in Development mode
- `environment`: Hosting environment name (`Development`, `Production`, etc.)

---

# SignalR (Real-Time)

SignalR is responsible for real-time communication and streaming.

SignalR builds on WebSocket where available and falls back to other transports automatically, so the real-time contract is expressed as hubs and strongly-typed messages rather than raw frames.

Clients connect to a single base endpoint and subscribe to the relevant groups/streams. Reconnection is handled by the SignalR client.

Examples include:

- streaming responses
- progress updates
- provider status
- notifications
- Workspace synchronization
- file changes
- permission requests

---

## Event Categories

Typical events include:

```
conversation.started

conversation.updated

conversation.completed
```

```
provider.status.changed
```

```
workspace.changed
```

```
diff.created
```

```
notification.created
```

---

# Streaming

Streaming responses should use SignalR (stream to a hub client) whenever practical.

Clients should begin rendering partial responses immediately.

---

# Pagination

Large collections should support pagination.

Typical parameters:

```
page

pageSize
```

---

# Filtering

Endpoints may expose filtering parameters.

Examples:

```
provider

workspace

conversation
```

---

# Sorting

Collections should support sorting.

Examples:

```
name

date

provider
```

---

# Long Running Operations

Long-running operations should return immediately.

Progress should be reported through SignalR events.

Examples:

- provider installation
- Workspace synchronization
- Git clone
- AI execution

---

# File Uploads

Binary uploads should use multipart/form-data.

Examples:

- images
- archives
- Workspace import

---

# File Downloads

Downloads should support:

- export conversation
- export Workspace
- backup
- logs

---

# Compatibility

The API should remain backward compatible whenever possible.

Breaking changes require:

- documentation
- version increment
- migration guidance

---

# Client Independence

The API should not expose implementation details.

Clients should not need to know:

- provider internals
- CLI commands
- storage implementation

---

# Future Evolution

Future versions may introduce:

- GraphQL
- Plugin endpoints
- Provider SDK
- External integrations

These additions should complement the REST API rather than replace it.

---

# References

Related documents:

- Product.md
- Architecture.md
- SecurityArchitecture.md
- DevelopmentStandards.md
- ADR-010 (SignalR for Real-Time Communication)
- ADR/