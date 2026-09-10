# Version 0.3 — Workspace Developer Tools & Git

## Objectives

Version 0.3 expands AI Agent Hub into a self-sufficient developer workspace environment:

- Native Git repository operations and direct workspace cloning
- Integrated studio file explorer with file management operations
- Lightweight in-browser code editor for quick inspection and edits
- Expanded multi-format file previews
- Single-user account management (update login username and password)

---

# Git Integration

Version 0.3 introduces native Git integration for local workspaces:

- **Repository Status**: View working directory status, modified, staged, and untracked files with visual badges.
- **Branch Management**: View current branch, create new branch, switch / checkout branches.
- **Commit Operations**: Stage changes, enter commit messages, and create local commits.
- **Remote Operations**: Push to remote, pull changes, fetch remote tracking branches.
- **Stash Support**: Stash working changes and apply/pop stashes.
- **Commit History**: Visual commit history log with author and timestamp.
- **Git Clone to Workspace**: Create a new workspace directly by providing a Git repository clone URL.

*Note: Git integration remains completely optional. The platform functions normally without Git installed.*

---

# Studio File Explorer

Integrated workspace file tree supporting:

- Create new file
- Create new folder
- Rename file or folder
- Delete file or folder (with safety confirmation)
- Drag and drop file/folder organization
- File search and path filtering within workspace tree

---

# Embedded Lightweight Editor

A lightweight in-browser editor designed for quick edits without replacing an external IDE:

- Syntax highlighting for popular programming languages
- Text search and replace
- Go-to file quick navigation
- Read-only viewing mode toggle
- Inline diff visualization against disk state

---

# Expanded File Previews

Enhanced preview support for additional file types:

- **Documents & Web**: PDF, HTML
- **Structured Data**: CSV, TSV
- **Logs & Config**: LOG, INI, TOML, ENV

---

# Single-User Account Management

Version 0.3 adds the ability for the authenticated administrator to update their account credentials directly from the settings interface without wiping or resetting database records:

- **Update Login Username**: Change the administrator username (`Current Password` required for authorization). Updates the persistent identity without affecting workspaces, conversations, or settings.
- **Update Password**: Change the administrator password (`Current Password`, `New Password`, and `Confirm New Password` required).
- **Single-User Scope**: Maintains the single-user architecture (strictly one administrator account in the system; multi-user RBAC remains deferred to Version 0.6).
- **Security Constraints**:
  - Current password verification is strictly enforced before any credential modification is persisted.
  - Endpoints are protected by rate limiting (`AuthLimiter`) and require an active authenticated administrator session.
  - Updating credentials immediately re-issues or updates the authenticated session cookie to ensure continuous access without unexpected logouts.
  - Recovery code reset via the 16-character token remains available as an emergency fallback on login.

