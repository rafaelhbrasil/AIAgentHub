# Version 0.4 — Multi-Pane Studio & Productivity

## Objectives

Version 0.4 empowers power users with advanced productivity layouts, parallel execution, and reusable prompt engineering:

- Studio multi-pane split layouts (dual chats, chat + editor, chat + live diff)
- Multi-provider parallel prompt execution and side-by-side comparison
- Command Palette (`Ctrl+K`) and comprehensive keyboard shortcuts
- Reusable Prompt Library with template variables and import/export
- Settings Danger Zone: password-based full system reset with confirmation

---

# Studio Multi-Pane Split Layouts

Flexible multi-pane layout options within the workspace studio:

- **Two Conversations**: View and prompt two independent conversation threads side-by-side.
- **Conversation & File Editor**: Chat with an agent on the left while reading or editing code files in the embedded editor on the right.
- **Conversation & Live Diff**: Chat with an agent while monitoring incoming file changes in real-time.
- **Side-by-Side Providers**: Prompt two different AI engines simultaneously and compare their responses in real-time.

---

# Multi-Provider Prompt Comparison

- Send a prompt simultaneously to multiple configured AI providers.
- Compare reasoning tokens, output quality, file diffs, and execution duration side-by-side.
- Choose which provider's generated changes to accept or merge into the workspace.

---

# Command Palette & Keyboard Shortcuts

- **Command Palette (`Ctrl+K` / `Ctrl+Shift+P`)**: Quick search and trigger any action, navigate workspaces, switch themes, and execute commands without mouse navigation.
- **Keyboard Shortcuts**:
  - Open Workspace: `Ctrl+O` / `Cmd+O`
  - Global Search / Go to File: `Ctrl+P` / `Cmd+P`
  - Switch Conversation: `Ctrl+Tab` / `Cmd+Tab`
  - New Conversation: `Ctrl+N` / `Cmd+N`
  - Accept Changes: `Ctrl+Enter` (in diff viewer)
  - Reject Changes: `Escape` / `Ctrl+Backspace`

---

# Reusable Prompt Library

A centralized catalog for managing recurring prompt templates:

- **Categorization & Tags**: Organize prompts into searchable categories.
- **Favorites**: Star frequently used prompts for instant access.
- **Dynamic Variables**: Define placeholders using `{{variable}}` syntax prompted upon insertion into chat.
- **Import & Export**: Export prompt libraries to JSON/YAML and import community prompt packs.

---

# Settings — Danger Zone: Full System Reset

A destructive full-reset entry point available from the Settings page (Account sub-tab), inside a dedicated **Danger Zone** section with danger styling. It mirrors the logged-out recovery wipe, but is reachable by an authenticated administrator and authorized by password only — no recovery code.

- **Authorization**: The confirmation dialog requests only the current administrator password. Nothing else (no recovery code, no typed confirmation phrase).
- **Confirmation Required**: A confirmation dialog must be shown before proceeding, listing both outcomes explicitly:
  - **Will be lost (everything in the app)**: workspaces, conversations, messages, providers, and settings — all application data stored in the database.
  - **Will NOT be deleted (data saved to disk)**:
    - Files saved to disk inside workspace folders
    - Git repositories on disk
    - Provider installations and configuration (external CLIs such as Claude, Antigravity, etc. are not uninstalled and their own configuration files are untouched)
- **Execution**: On confirmation the server performs a full database wipe (`WipeAllDataAsync`), the system returns to Setup Mode, and the administrator session is signed out.
- **Post-Reset Flow**: The client is logged out and redirected to the first-run setup wizard (initial administrator creation), exactly like the logged-out reset flow.
- **Security Constraints**: Endpoint requires an active authenticated administrator session, is protected by rate limiting (`AuthLimiter`), and verifies the current password before wiping. Incorrect passwords are rejected without any data being destroyed.
