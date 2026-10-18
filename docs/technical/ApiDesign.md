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

PUT /api/v1/auth/account/username

PUT /api/v1/auth/account/password
```

Account management endpoints allow the authenticated administrator to update credentials:
- `PUT /api/v1/auth/account/username`: Request payload `{ "currentPassword": "...", "newUsername": "..." }`. Validates current password, updates the administrator username, and refreshes the authenticated session cookie.
- `PUT /api/v1/auth/account/password`: Request payload `{ "currentPassword": "...", "newPassword": "...", "confirmNewPassword": "..." }`. Validates current password and matching confirmation, re-hashes password with fresh salt, updates credentials, and refreshes the session cookie.
Both endpoints are protected by `AuthLimiter` rate limiting.

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

POST /api/v1/workspaces/clone

GET /api/v1/workspaces/{id}

GET /api/v1/workspaces/{id}/download

DELETE /api/v1/workspaces/{id}
```

Workspace cloning endpoint:
- `POST /api/v1/workspaces/clone`: Request payload `{ "cloneUrl": "...", "targetDirectory": "...", "workspaceName": "..." }`. Runs `git clone` into the specified target directory and registers the workspace with `Origin = WorkspaceOrigin.Git`.

---

## Filesystem

Examples:

```
GET /api/v1/filesystem/drives

GET /api/v1/filesystem/browse?path={path}

GET /api/v1/filesystem/tree?workspaceId={workspaceId}

GET /api/v1/filesystem/forbidden-paths

POST /api/v1/filesystem/mkdir

GET /api/v1/filesystem/file?workspaceId={workspaceId}&path={path}

POST /api/v1/filesystem/file

PUT /api/v1/filesystem/file

PUT /api/v1/filesystem/rename

DELETE /api/v1/filesystem/item

POST /api/v1/filesystem/move
```

Filesystem operations:
- `GET /api/v1/filesystem/file?workspaceId={workspaceId}&path={path}`: Retrieves file content for lightweight in-browser editor inspection and editing, including binary check and length.
- `POST /api/v1/filesystem/file`: Request payload `{ "workspaceId": "...", "path": "...", "content": "..." }`. Creates a new empty or seeded file within workspace root.
- `PUT /api/v1/filesystem/file`: Request payload `{ "workspaceId": "...", "path": "...", "content": "..." }`. Saves modified file content from the embedded code editor.
- `PUT /api/v1/filesystem/rename`: Request payload `{ "workspaceId": "...", "oldPath": "...", "newPath": "..." }`. Renames a file or folder within workspace root.
- `DELETE /api/v1/filesystem/item`: Request payload or query `{ "workspaceId": "...", "path": "..." }`. Deletes a file or directory within workspace root.
- `POST /api/v1/filesystem/move`: Request payload `{ "workspaceId": "...", "sourcePath": "...", "destinationDirectory": "..." }`. Moves a file or folder to support drag-and-drop workspace reorganization.
All filesystem endpoints enforce strict containment within the workspace root path to prevent path traversal vulnerabilities.

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

POST /api/v1/conversations/{id}/switch-provider

GET /api/v1/conversations/{id}/sessions

PUT /api/v1/conversations/{id}/pin

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

PUT /api/v1/providers/{id}/settings

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
GET /api/v1/preview?workspaceId={workspaceId}&path={path}

GET /api/v1/files/{id}/preview
```

The server-side content rendering architecture supports multiple file preview categories:
- **Code & Text**: C#, TS, JS, Python, HTML, CSS, SQL, Shell, Markdown
- **Documents & Web**: PDF (`application/pdf` embed/data URI view), HTML (sandboxed preview)
- **Structured Data**: CSV, TSV (rendered as responsive data table)
- **Logs & Config**: LOG, INI, TOML, ENV (colorized key-value/log stream)
- **Images**: PNG, JPG, WEBP, SVG, GIF, BMP

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

POST /api/v1/skills/sync-symlinks
```

---

## Git Operations

Git integration enables repository control directly from the workspace studio. When Git is not installed on the server machine, endpoints gracefully report `isGitInstalled: false` without failing.

Examples:

```
GET /api/v1/git/status?workspaceId={workspaceId}

GET /api/v1/git/branches?workspaceId={workspaceId}

POST /api/v1/git/branch

POST /api/v1/git/checkout

POST /api/v1/git/stage

POST /api/v1/git/unstage

POST /api/v1/git/commit

POST /api/v1/git/push

POST /api/v1/git/pull

POST /api/v1/git/fetch

POST /api/v1/git/stash

GET /api/v1/git/history?workspaceId={workspaceId}&maxCount={maxCount}
```

Endpoint contracts:
- `GET /api/v1/git/status`: Returns `{ isGitInstalled, isRepository, currentBranch, ahead, behind, staged: [{ path, status }], modified: [{ path, status }], untracked: [string] }`.
- `GET /api/v1/git/branches`: Returns list of `{ name, isCurrent, isRemote }`.
- `POST /api/v1/git/branch`: Request payload `{ "workspaceId": "...", "branchName": "...", "checkout": true }`. Creates a new Git branch and optionally checks it out.
- `POST /api/v1/git/checkout`: Request payload `{ "workspaceId": "...", "branchName": "..." }`. Checks out an existing local or tracking branch.
- `POST /api/v1/git/stage`: Request payload `{ "workspaceId": "...", "files": ["..."], "stageAll": false }`. Stages specific files or all modified/untracked files.
- `POST /api/v1/git/unstage`: Request payload `{ "workspaceId": "...", "files": ["..."], "unstageAll": false }`. Unstages specific files or all staged changes.
- `POST /api/v1/git/commit`: Request payload `{ "workspaceId": "...", "message": "...", "stageAll": false }`. Creates a local Git commit with the specified message.
- `POST /api/v1/git/push`: Request payload `{ "workspaceId": "...", "remote": "origin", "branch": "..." }`. Pushes local commits to the upstream remote repository.
- `POST /api/v1/git/pull`: Request payload `{ "workspaceId": "...", "remote": "origin", "branch": "..." }`. Pulls changes from the upstream remote repository.
- `POST /api/v1/git/fetch`: Request payload `{ "workspaceId": "..." }`. Fetches remote refs and branch updates without merging.
- `POST /api/v1/git/stash`: Request payload `{ "workspaceId": "...", "action": "save"|"pop"|"apply"|"list", "message": "..." }`. Performs stash operations.
- `GET /api/v1/git/history`: Returns list of recent commits `{ commitHash, shortHash, author, email, date, message }`.

---

## Prompt Library

Examples:

```
GET /api/v1/prompts

POST /api/v1/prompts

GET /api/v1/prompts/{id}

PUT /api/v1/prompts/{id}

DELETE /api/v1/prompts/{id}
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

Clients connect to a single base endpoint (`/hubs/agent`) and subscribe to conversation groups scoped per user (`usr_{userId}_conv_{conversationId}`). Reconnection and group re-subscription are handled by the SignalR client. Streaming tokens, heartbeat pings, completion events, diff notifications, and permission prompts are delivered strictly to the matching user+conversation group, preventing cross-tab or cross-user response bleeding.

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