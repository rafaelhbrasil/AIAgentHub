# Version 0.3 — Workspace Developer Tools & Git

## Objectives

Version 0.3 expands AI Agent Hub into a self-sufficient developer workspace environment:

- Native Git repository operations and direct workspace cloning
- Integrated studio file explorer with file management operations
- Lightweight in-browser code editor for quick inspection and edits
- Expanded multi-format file previews

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

