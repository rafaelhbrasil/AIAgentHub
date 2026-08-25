# Mobile UX, Full-Screen Diff Reviewer, and Word-Wrap Toggle Specification

## Overview
This specification addresses mobile responsive layout improvements, full-screen diff modal coverage, smart added/deleted file pane rendering, unified/in-line diff defaulting on mobile, mobile side-by-side pane division tabs, and an interactive word-wrap toggle for code review.

---

## Requirements

### 1. Mobile Prompt Docking & Viewport Containment
- **Problem**: When `ChangesOverviewBar` expands or when messages scroll, outer container elements scroll vertically on mobile devices, pushing the prompt bar up or down.
- **Specification**:
  - Enforce `height: 100dvh`, `max-height: 100dvh`, and `overflow: hidden` on `.app-root`, `.app-main`, and `.studio-root` on mobile devices (`<= 768px`).
  - `.chat-container` occupies `flex: 1 1 auto; min-height: 0; overflow: hidden;`.
  - ONLY `.chat-messages` scrolls vertically (`overflow-y: auto; overscroll-behavior: contain; -webkit-overflow-scrolling: touch;`).
  - `.chat-bottom-dock` has `flex-shrink: 0;` and remains permanently docked at the bottom of the mobile viewport.

### 2. Full-Screen Diff Reviewer Modal on Mobile
- **Specification**:
  - On mobile (`<= 768px`), the Diff Viewer modal expands to cover **100% of the viewport**:
    ```css
    position: fixed;
    inset: 0;
    width: 100vw !important;
    height: 100dvh !important;
    max-width: 100vw !important;
    max-height: 100dvh !important;
    border-radius: 0 !important;
    margin: 0 !important;
    border: none !important;
    z-index: 9999;
    ```
  - Modal overlay padding is set to 0 on mobile.
  - The diff viewer body, tabs, code viewer, and action buttons dynamically fit within `100dvh` without overflowing or showing double scrollbars.

### 3. Smart Handling of Added, Deleted, and Modified Files
- **Added Files (`DiffChangeType.Created`)**:
  - Automatically display **only the Modified (After)** pane with green addition lines at 100% height (hiding the empty baseline pane).
- **Deleted Files (`DiffChangeType.Deleted`)**:
  - Automatically display **only the Original (Before)** pane with red deletion lines at 100% height (hiding the empty modified pane).
- **Mobile Default View Mode**:
  - On mobile devices (`window.innerWidth <= 768px`), default the diff view mode to **Unified / In-Line Diff** (`viewMode = 'unified'`), providing a clean single-column diff.
- **Side-by-Side Mobile Sub-View Tabs**:
  - When in Side-by-Side mode on mobile for modified files, provide segmented view options:
    - **`Modified (After)`**: Displays only the new version at full height.
    - **`Original (Before)`**: Displays only the baseline version at full height.
    - **`Split 50/50`**: Displays both versions stacked in equal 50/50 height panes.

### 4. Word-Wrap / Line-Break Toggle
- **Specification**:
  - Add a **`↩ Wrap` / `➡ No Wrap`** toggle button in the Diff Viewer header next to the view mode selector.
  - When Wrap is ON:
    - Code lines apply `white-space: pre-wrap; word-break: break-all; overflow-wrap: anywhere;`.
  - When Wrap is OFF:
    - Code lines apply `white-space: pre; word-break: normal; overflow-x: auto;`.
  - The user's wrap preference is persisted in `localStorage` under `agenthub_diff_word_wrap`.

---

## Component Updates
1. **`src/AIAgentHub.Web/frontend/src/index.css`**:
   - Enforce 100dvh lock on mobile containers (`.app-root`, `.app-main`, `.studio-root`, `.chat-container`).
   - Style full-screen modal behavior on mobile (`.modal-box`, `.modal-overlay`, `.diff-modal-body`, `.diff-viewer-scroll-container`).
   - Add styles for word wrap (`.diff-wrap-enabled`) and mobile side-by-side pane splitters (`.diff-pane-full`, `.diff-pane-half`).
2. **`src/AIAgentHub.Web/frontend/src/components/modals/DiffViewerModal.tsx`**:
   - Default to `'unified'` on mobile (`window.innerWidth <= 768`).
   - Add word-wrap state and localStorage persistence.
   - Add smart single-pane rendering for Created and Deleted files.
   - Add mobile sub-view tab toggle (`'modified' | 'original' | 'split'`) when in side-by-side mode.
   - Add Wrap toggle button to header.
3. **`src/AIAgentHub.Web/frontend/src/components/workspaces/WorkspaceStudioView.tsx`**:
   - Pass `'full'` modal size when opening `DiffViewerModal`.
