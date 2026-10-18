# 🧪 UI Smoke Test Report

- **Date & Time:** 2026-10-07 22:34 (Local)
- **User:** Rafael Brasil
- **Target URL:** `https://localhost:5432`
- **Mode:** `Default (Happy Path & Dialogs)`
- **Overall Status:** ✅ Passed (1 issue found → fixed → re-verified)

**Scope:** Pages affected by commits since `26628a5` (05/10): `/login` (SignInPage, SetupWizard), `/settings` (SettingsView), `/workspaces` (WorkspacesView, Clone/Open modals, FileTree), `/workspaces/01094ced-ced0-4120-ae1d-b307c73eb6cb` (WorkspaceStudioView, StudioHeader/Sidebar/GitPanel, FilePreviewModal, CodeEditorModal).

---

## 📊 Smoke Test Scorecard

| Gate / Check | Scope | Result | Details |
|---|---|---|---|
| **Server Lifecycle** | `launchSettings.json` (5432) | ✅ Healthy | Spawned PID 37136 (port was not listening); killed at teardown |
| **Network Integrity** | Static assets & API calls | ✅ Passed | All routes: 0 failed assets; git/filesystem/settings/auth APIs all 200. One `401 GET /api/v1/auth/session` observed after explicit logout = correct unauthenticated semantics (client rendered login gracefully, no JS error) |
| **Console Hygiene** | JavaScript runtime | ✅ Clean | 0 uncaught errors / 0 unhandled rejections (post-fix re-verification included) |
| **Standard Viewports** | 1920x1080, 768x1024, 390x844, 360x800 | ✅ Passed | No horizontal overflow on any route/viewport, before and after fix |
| **Extended Viewports** | 2560x1440 … 320x568 (`--full` only) | ⏭ Skipped | Not requested (Default mode) |
| **Layout Compatibility** | Buttons, Modals, Hamburger | ✅ Passed | Sign In/Save/Add Workspace buttons visible+in-viewport everywhere; modals (Recovery, Clone, Open/Create, FilePreview, CodeEditor) centered (centerDiff 0) and fit viewport; hamburger visible ≤768px on authenticated routes. `.studio-mobile-nav` button hidden at 1920 = by design (mobile bottom nav, `display:flex` appears ≤768) |
| **Text Readability** | Text overlap & out-of-view text (all modes) | ✅ Passed (after fix) | F1 resolved (see Fixes). Zero out-of-view text on all routes/viewports |
| **Critical Scenarios** | Happy path & dialogs | ✅ Passed | Login (POST /auth/login 200 → nav renders), logout→SignInPage roundtrip, Recovery modal open/close, Settings section switch ×3 + Save (PUT 200), Clone Workspace modal, Open/Create modal, FilePreview→CodeEditor modal chain, Git drawer (status/branches/history 200), link peek `#recover` (no 404) |
| **Negative Tests** | `--full` validations | ⏭ Skipped | Not requested (Default mode) |
| **Setup Wizard** | SetupWizardModal | ⏭ Not exercised | `{"isSetupCompleted":true}` — wizard not triggerable in current DB state |

**Route-specific verification (de8b0d2 autocomplete fix):** SignInPage inputs carry `autocomplete="username"` and `autocomplete="current-password"` ✅.

---

## 🔧 Fixes (Before → After)

| Check | Viewport | Before (failure) | After (fix) | Status |
|---|---|---|---|---|
| F1 text-overlap (header logo × version badge) | 360x800 (also flagged @390x844 on all authenticated routes) | Before _(image removed by user)_ | After _(image removed by user)_ | ✅ RESOLVED |

**F1 resolution detail:**
- **Root cause:** `.logo-text` shrinks inside `.header-left` flex at ≤390px → `.logo-title` wrapped to 2 lines and `.logo-version` wrapped to 2 lines. Per-line `getClientRects()` showed **zero** real glyph intersections (union-rect artifact of wrapped inline elements), but the union `getBoundingClientRect()` pair (1188px², 36.7% of smaller) fails the Gate 2.6 thresholds — and a 4-line wrapped logo header is a genuine responsiveness defect.
- **Fix** (`src/AIAgentHub.Web/frontend/src/index.css`):
  - `.logo-title { white-space: nowrap; }` — title never wraps
  - `@media (max-width: 768px) { .logo-version { display: none; } }` — decorative version badge hidden on mobile/tablet (frees header space, follows existing `.user-name` hide convention in same block)
- **Build:** `npm run build` (frontend only) → `wwwroot/assets` regenerated
- **Retest (shared-code exception — all dependent pages re-run):** settings, workspaces, studio, login × viewports 1920/768/390/360 → zero overlaps, zero overflow, buttons visible, hamburger visible, 0 console errors. Version badge confirms `display: none` ≤768 (fresh CSS served).

📸 **Screenshots:**
- Desktop Viewport _(image removed by user)_
- Tablet Viewport _(image removed by user)_
- Mobile Viewport _(image removed by user)_
