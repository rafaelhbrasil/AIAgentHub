# Version 0.2 — Multi-Provider Flexibility & Chat DX

## Objectives

Version 0.2 delivers the core AI orchestration flexibility and chat developer experience of AI Agent Hub:

- In-conversation AI provider switching with intelligent context migration and differential replay
- Independent N-to-N session tracking per provider (`ConversationProviderSession`)
- Chat input autocomplete for Skills (`/`) and workspace files/folders (`@`)
- Dedicated provider settings, model configuration, and visibility controls
- Folder creation directly within the workspace folder navigator
- Dark, Light, and System theme support

---

# Workspace Improvements

## Visual Folder Navigator (Workspace Creation)

- **New Folder Creation**: Create directories directly inside the visual folder navigator modal during workspace setup.
- **Quick Navigation**: Windows-style quick access shortcuts, drive switching, and breadcrumb traversal.

---

## Favorites & Recent Workspaces

- Favorite Workspaces appear prominently on the Dashboard and workspace selector.
- Recently opened Workspaces displayed with timestamp and quick-access cards.

---

## Workspace Search & Archive

- Search workspaces by name, path, or tags.
- Archive inactive Workspaces without deleting them from disk or database history.

---

# AI & Multi-Provider Architecture

## In-Conversation Provider Switching

Users can switch AI providers mid-conversation seamlessly.

### Default Model Reset
- When switching to a new provider, the active model automatically defaults to `"Default"` (delegating to the new provider's default model / fallback).
- Users can change the active model at any time via the model dropdown in the studio header.

### Context Migration & Replay Protocol
- **Context Injection**: When switching to a new provider (or when executing a prompt on a provider with unshared prior turns), AgentHub compiles the unshared conversation history (previous user prompts, assistant responses, and workspace file changes) and injects it as a structured handoff preamble into the provider CLI prompt on its first turn.
- **Dynamic Default Provider & Single-Provider Guardrails**:
  - The default provider is dynamically resolved at runtime as the first operational, ready-to-use provider (`Status == Ready`, not hidden). No hardcoded fallback strings are assumed.
  - If no operational AI provider is available on the machine, warning banners with direct navigation links to the `/providers` page are displayed, and conversation execution gracefully alerts the user.
  - Switching providers is only permitted when at least two operational providers exist. If only one provider is ready, attempting to switch displays an informative modal explaining that only one engine is active and provides a direct link to the Providers management page.
- **Migration Dialog & Scope Ordering**:
  - When initiating a provider switch, a dialog prompts the user to select how much history to transfer, strictly ordered by interaction count:
    1. **Differential (`delta`)**: First and default; transfers only unshared interactions (user prompt + assistant response turns) since the target provider's last active checkpoint.
    2. **Recent $N$ Interactions**: Configured in `appsettings.json` via `ProviderSwitchSettings:RecentMessageCounts` (default: `[10, 20, 50]`). Each count represents 1 interaction turn (1 user prompt + 1 assistant response).
    3. **Full (`all`)**: Complete conversation history (all interactions).
    4. **None (`none`)**: Fresh session (0 interactions). When None is selected, the target provider's checkpoint is **not** immediately updated. If the user performs no prompts in the new provider and switches away or returns, all previous history remains available for migration. Only upon the first executed prompt in the new provider is its session checkpoint established.
  - **Redundant Scope Disabling**: When Differential interaction count $D$ is smaller than or equal to an option's threshold ($D \le N$ or $D < \text{totalInteractions}$ for Full), those higher options are disabled and annotated with `"(previously migrated)"` to prevent duplicate message replay.
- **Conversation Locking & In-Progress Migration Guard**:
  - While provider switching is in progress, the conversation enters `SwitchingProvider` status in the database. Prompt inputs, model selectors, and standard switch dialogs are locked across page reloads.
  - If the user clicks the active provider button in the header while a migration is in progress, an informative modal appears advising that migration is active. The user may choose to wait or **Abort & Revert**, cancelling the switch, restoring the previous provider/model, and returning the conversation to `Active` status. If migration completes while the dialog is open, the modal automatically closes.

### N-to-N Conversation-Provider Session Tracking
- **Session Mapping (`ConversationProviderSession`)**:
  - Conversations maintain independent CLI session records for each provider used within that conversation (`ConversationId`, `ProviderId`, `ProviderSessionId`, `LastSharedMessageId`, `LastActiveAtUtc`).
- **Bidirectional Smart Syncing**:
  - When switching from Provider A to Provider B and later returning to Provider A, Provider A already possesses the conversation history up to its `LastSharedMessageId`.
  - AgentHub calculates the message diff (only the prompts and responses generated in Provider B) and synchronizes only the unshared diff to Provider A.
- **Per-Message Provider Attribution**:
  - Every message records `OriginProviderId` and `OriginModelId` to visually identify which AI engine generated each response.

---

## Granular Permission Requests

Version 0.2 introduces explicit, transparent user approval for sensitive provider operations:

- editing files
- deleting files
- executing terminal commands
- creating files

Exposes permission prompts generated by providers and supports cancelling or aborting running operations.

---

# Chat Input & Autocomplete

## Slash (`/`) Skills Autocomplete

- Typing `/` in the prompt input bar opens an inline autocomplete popup displaying available Skills with descriptions and arguments.
- Keyboard navigation (`↑`/`↓`, `Enter`, `Tab`, `Escape`) to quickly insert commands.

## At (`@`) File & Folder Mentions

- Typing `@` opens an inline filter for files and folders within the workspace scope, automatically completing relative paths.

---

# Provider Management & Settings

Dedicated Provider Settings modal/page (accessible via ⚙ gear icon on provider cards or in Settings):

- **Strict Provider Availability Filtering**: Only operational and ready-to-use providers (`Status == Ready`, installed, authenticated, non-discontinued, and not hidden) are listed anywhere in the user interface (conversation studio header, provider switching modal, create conversation modal, and workspace setup). Discontinued, uninstalled, unauthenticated, error-state, or hidden providers are exclusively listed within the Providers management tab where they can be configured, authenticated, or installed.
- **Hide / Unhide Provider**: Toggle provider visibility to hide unused providers from the studio selector and dashboard.
- **Model Customization**: View dynamic models discovered from CLI, toggle model visibility (`IsDisplayed`), set default model, and configure default reasoning/thinking effort level.
- **Version & Health Status**: View installed binary path, CLI version, authentication probe status, and execution mode (Headless vs. Headed console).

---

# Security & Server Administration

## Safe Client Parameter (`--safe-client <IP>`)

Version 0.2 introduces the `--safe-client <IP>` CLI startup parameter (with aliases `--safeclient`, `-safe-client`, `/safe-client`, `--safe-ip`):

- **Localhost-Equivalent Remote Client**: When specified, incoming requests from the configured IP address receive identical administrative permissions as `localhost` (loopback).
- **Network Mode Bypass**: When LAN access is disabled (`NetworkMode.Localhost`), the Safe Client IP is permitted through server middleware to access the web UI and APIs.
- **Initial Setup Access**: If the server has not yet completed initial setup, the Safe Client IP can run the setup wizard to create the initial administrator account.
- **Emergency Recovery Support**: When run in tandem with `--recovery`, the Safe Client IP can perform unassisted database resets and complete database wipes without requiring a recovery code.
- **Default (Without Parameter)**: Without the `--safe-client` flag, local permissions and unassisted recovery remain restricted exclusively to the loopback interface (`127.0.0.1` / `::1`).

---

# User Interface & Themes

- **Themes**: Light, Dark, and Follow System themes with smooth transition and persistent preference.
- **Conversation Management**: Pin and organize conversations (pinned items ordered by most recent interaction first, followed by remaining conversations).
---