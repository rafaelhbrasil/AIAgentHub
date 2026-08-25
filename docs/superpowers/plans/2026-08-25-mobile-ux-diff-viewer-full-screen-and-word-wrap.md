# Mobile UX, Full-Screen Diff Reviewer, and Word-Wrap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix mobile prompt box docking so it never scrolls out of view, make the Diff Viewer modal use 100% of the screen on mobile, default to in-line diff on mobile, support smart single-pane for added/deleted files and segmented view tabs for modified files, and add a word-wrap toggle in the diff header.

**Architecture:** Update CSS layout rules in `index.css` for strict 100dvh mobile containment and full-screen modal styling, update `DiffViewerModal.tsx` to handle mobile default view mode, wrap toggle with localStorage, smart added/deleted single-pane rendering, and mobile side-by-side tabs.

**Tech Stack:** React, TypeScript, CSS (100dvh, flexbox, grid, media queries), Vitest.

---

### Task 1: Update `DiffViewerModal.tsx` with Word-Wrap, Mobile Default Mode, and Smart File View

**Files:**
- Modify: `src/AIAgentHub.Web/frontend/src/components/modals/DiffViewerModal.tsx`
- Modify: `src/AIAgentHub.Web/frontend/src/components/workspaces/WorkspaceStudioView.tsx`

- [ ] **Step 1: Update `DiffViewerModal.tsx`**
  - Initialize `viewMode`: default to `'unified'` if `window.innerWidth <= 768`, otherwise `'sideBySide'`.
  - Initialize `isWordWrap`: read boolean from `localStorage.getItem('agenthub_diff_word_wrap') === 'true'` (default `true` on mobile, `false` on desktop).
  - Add `sideBySideMobileTab`: `'modified' | 'original' | 'split'` (default `'split'`).
  - For `DiffChangeType.Created`: display only the Modified (After) pane.
  - For `DiffChangeType.Deleted`: display only the Original (Before) pane.
  - Add Word-Wrap toggle button in header.
  - In Side-by-Side mode on mobile for modified files, display sub-view tabs (`Modified`, `Original`, `Split`).

- [ ] **Step 2: Update `WorkspaceStudioView.tsx`**
  - Pass `'full'` size to `showModal` for `DiffViewerModal`.

---

### Task 2: Update `index.css` for 100dvh Mobile Containment, Full-Screen Modal, and Wrap Styles

**Files:**
- Modify: `src/AIAgentHub.Web/frontend/src/index.css`

- [ ] **Step 1: Add mobile layout viewport lock**
  - Enforce `height: 100dvh`, `max-height: 100dvh`, and `overflow: hidden` on `.app-root`, `.app-main`, and `.studio-root`.
  - Ensure `.chat-container` occupies `flex: 1 1 auto; min-height: 0; overflow: hidden;`.
  - Ensure `.chat-messages` is the only vertically scrolling element.
  - Ensure `.chat-bottom-dock` is permanently docked at the bottom with `flex-shrink: 0`.

- [ ] **Step 2: Add full-screen modal styles on mobile**
  - `.modal-box.modal-full`, `.modal-box.modal-xl` take `100vw` / `100dvh` with 0 margins, 0 border-radius, and 0 padding on overlay.
  - `.diff-modal-body` and `.diff-viewer-scroll-container` adapt to full viewport height.

- [ ] **Step 3: Add word-wrap styles**
  - `.diff-wrap-enabled .diff-line` applies `white-space: pre-wrap !important; word-break: break-all; overflow-wrap: anywhere;`.

---

### Task 3: Build & Verification

- [ ] **Step 1: Run frontend build and tests**
Run: `npm run build -w aiagenthub-frontend && npm run test:frontend`
- [ ] **Step 2: Run full test suite**
Run: `npm test`
