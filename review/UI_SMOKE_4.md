# 🧪 UI Smoke Test Report

- **Date & Time:** 2026-10-08 12:26:40 (Local)
- **User:** Rafael Brasil
- **Target URL:** `https://localhost:5432/workspaces/629f5667-499c-40c7-b91a-cff36b205a3f/conversations/72a761ec-8155-45d7-9aec-a37790a96649`
- **Mode:** `Quick` (+ `--screenshot`)
- **Overall Status:** ✅ Passed (after fix loop) — 2 defects found during post-capture visual audit, fixed & re-verified; Gate 2.6 spec flags analyzed as checker artifacts

---

## 📊 Smoke Test Scorecard

| Gate / Check | Scope | Result | Details |
|---|---|---|---|
| **Server Lifecycle** | `launchSettings.json` (5432) | ✅ Healthy | Spawned PID 24440 (port free at start); killed at teardown |
| **Network Integrity** | Static assets & API calls | ✅ Passed | 22 requests (incl. fonts), 0 failed (0 4xx/5xx); filesystem/tree, conversations, git/status, diffs, skills all 200 |
| **Console Hygiene** | JavaScript runtime | ✅ Clean | 0 uncaught errors, 0 unhandled rejections (verified at start + after all viewports; one mid-run page reload produced 0 errors) |
| **Standard Viewports** | 1920x1080, 768x1024, 390x844, 360x800 | ✅ Passed | `scrollWidth > innerWidth` false on all 4; ≥300ms stabilization before each evaluation |
| **Extended Viewports** | 2560x1440 … 320x568 | ⏭ Skipped | `--full` only |
| **Layout Compatibility** | Buttons, Modals, Hamburger | ⏭ Skipped | Default/`--full` only (Quick mode) |
| **Text Readability** | Text overlap & out-of-view text | ⚠️ Spec-flagged → 0 real | See analysis below; **out-of-view: 0 offenders on all 4 viewports** |
| **Critical Scenarios** | Happy path & dialogs | ⏭ Skipped | Quick mode (no form/modal/link execution) |
| **Negative Tests** | `--full` validations | ⏭ Skipped | Not requested |

**Auth:** route loaded with existing session (persistent profile); no login required.

---

## 🔍 Gate 2.6 Overlap Analysis (spec-flagged pairs — all classified checker artifacts)

| Viewport | Spec pairs | Actual (clip-aware) | Nature of flags | Visual verdict |
|---|---|---|---|---|
| 1920x1080 | 10 | **0** | FileTree rows × Conversations list — sidebar file-tree is an `overflow` scroller; rows scrolled-out of its clip rect intersect the list below in raw rects | ✅ Clean — evidence _(image removed by user)_ shows file-tree clipped above Conversations, no glyph collision |
| 768x1024 | 4 | **0** | Inline `CODE`/`STRONG` wraps inside chat message text (union-rect of multi-line inline elements) inside `.chat-messages` scroller | ✅ Clean (same class as UI_SMOKE_2 F1 analysis) |
| 390x844 | 3 | **0** | (a) Branch chip box extends under Git button but chip text is **ellipsized** (`feature/...`) — no visible glyph in overlap zone; (b)+(c) chat inline wraps | ✅ Clean — evidence _(image removed by user)_ (supplementary analysis shot) |
| 360x800 | 10 | **0** | Sticky header (logo/avatar) × scrolled chat content — dual-sticky-context + `.chat-messages` overflow clip (same class as UI_SMOKE_3 F5) | ✅ Clean (same verified class) |

**Root cause of flags (checker spec, not app):** Gate 2.6's skip-list covers `aria-hidden`/`opacity`/`sr-only`/containment/single-cross-sticky, but not (1) text clipped by `overflow` scrollers, (2) element boxes under `text-overflow: ellipsis`, (3) union-rects of wrapped inline elements, (4) pairs inside *different* sticky contexts. All four classes were visually verified as non-defects.

**Out-of-view text:** 0 offenders at every viewport (per spec: skip `overflow-x: auto|scroll` only).

**Suggestion:** extend Gate 2.6 skip rules with overflow-clip ancestor + distinct-sticky-context + per-line `getClientRects()` comparison to eliminate this artifact class in future runs.

---

## 📸 Screenshots (`review/UI_SMOKE_4/`)

- **Before/analysis (text-overlap @1920):** ui_smoke_fail_text-overlap_1920x1080.png _(image removed by user)_
- **Supplementary (chip×Git @390):** ui_smoke_fail_chip_git_390x844.png _(image removed by user)_
- **Previews:**
  - Desktop Viewport _(image removed by user)_
  - Tablet Viewport _(image removed by user)_
  - Mobile Viewport _(image removed by user)_

---

## 🔧 Fixes (Before → After)

Found during human review of the captured mobile preview (not detected by any gate — see Detection Gaps). Shared-code retest after both fixes: overflow/overlap/timestamp checks × 1920/768/390/360 all clean, 0 console errors, frontend 96/96.

| Check | Viewport | Before (failure) | After (fix) | Status |
|---|---|---|---|---|
| F6: Chat/Files/Git tab row clipped, no scroll (`.studio-mobile-nav` scrollW 425 > clientW 378, `overflow-x: visible` + `body overflow-x: hidden` → Git tab tail unreachable) | 390x844 | Before _(image removed by user)_ | After _(image removed by user)_ — nav `overflow-x: auto`, scrollLeft reaches max 47px | ✅ RESOLVED |
| F7: assistant message timestamps wrap mid-number (`18:08: 40`, height 37px = 2 lines) — `.message-header` too wide at 390 (name + model badge + time) | 390x844 | (visible in Before image above: `18:11: 28`) | same After image — all timestamps 19px single line; fix: `.message-header { flex-wrap: wrap }` + last-child `white-space: nowrap` | ✅ RESOLVED |

**Fixes applied in:** `src/AIAgentHub.Web/frontend/src/index.css` → `npm run build` + `npm run test:frontend` (96/96).

---

## 🔎 Detection Gaps (why smoke runs 1–4 missed F6/F7)

1. **F6 (tab row) — Gate 2.6 out-of-view defeated by CSS computed-value coercion:** `.app-main` sets `overflow-y: auto`; per CSS spec a visible `overflow-x` on the same element **computes to `auto`** → the checker's "skip ancestors with `overflow-x: auto|scroll`" rule matched `.app-main` and skipped *all* main content on mobile. Additionally `.studio-root{overflow-x:hidden}` clipped silently. Spec re-run post-fix still skips it — but now legitimately (nav is a real scroller).
2. **F6 — data-dependent:** overflow needs this workspace's long branch name in the tab label (`Git (feature/v0.3_testing_and_fixes)`, 425px). Runs 1–3 tested other routes (Calc / branch `main` → fits). Run 4 was the first on this route — and its checker couldn't flag it per gap 1.
3. **F7 (timestamp wrap) — invisible to all gates by definition:** wrapping is neither overlap, nor out-of-view, nor horizontal overflow. No mode (`--quick`/default/`--full`) measures "text must not break mid-token". Only visual audit of captured screenshots catches it — captures were saved but never reviewed after the run.
4. **Missed hint:** run-4's 390 spec overlap `chip × Git button` was a symptom of the same crowded tab/header row; classified as ellipsis-clip artifact after verifying only the chip's text, not the row's horizontal overflow.

**Skill suggestions:** (a) treat computed `overflow-x: auto` + `overflow-y` coercion as non-skip when the element also has visible content exceeding viewport; (b) add a "timestamp/mono-token wrap" heuristic (height > 1.6× line-height on `white-space`-nowrap-intended nodes) or mandatory human review step for `--screenshot` previews.
