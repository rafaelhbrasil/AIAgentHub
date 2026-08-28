# Version 0.4 — Multi-Pane Studio & Productivity

## Objectives

Version 0.4 empowers power users with advanced productivity layouts, parallel execution, and reusable prompt engineering:

- Studio multi-pane split layouts (dual chats, chat + editor, chat + live diff)
- Multi-provider parallel prompt execution and side-by-side comparison
- Command Palette (`Ctrl+K`) and comprehensive keyboard shortcuts
- Reusable Prompt Library with template variables and import/export

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
