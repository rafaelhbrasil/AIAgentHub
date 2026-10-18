# 🧪 UI Smoke Test Report — Run 3 (write-flow gap test + backend suite)

- **Date & Time:** 2026-10-08 10:17:18 (Local)
- **User:** Rafael Brasil
- **Target URL:** `https://localhost:5432`
- **Mode:** `Default (Happy Path & Dialogs)` + write-flow deepening
- **Overall Status:** ⚠️ Passed with 1 unresolved backend failure (F2, out of smoke-fix scope) — 2 UI/backend defects found & fixed (F3, F4)

**Scope:** Continuation of UI_SMOKE_2 _(image removed by user)_ (F1 resolved there). This run covers the previously identified gaps: shared-header post-fix sweep, conversation-route write flows (FileTree, CodeEditor, Git), backend v0.3 test suite.

---

## 📊 Smoke Test Scorecard

| Gate / Check | Scope | Result | Details |
|---|---|---|---|
| **Server Lifecycle** | `launchSettings.json` (5432) | ✅ Healthy | Spawned PID 12976 → restarted 79368 (after backend fix); killed at teardown |
| **Backend unit tests** | `npm run test:unit` (v0.3 tests incl.) | ✅ Passed | **315/315** — re-run AFTER F3 fix: still 315/315 |
| **Backend integration tests** | `npm run test:integration` (watchdog: spawn + stall-kill at 300s + total 1500s) | ⚠️ 49 passed / **1 failed** / 1 skipped (51) | Failure = **F2** (below). Build-vs-server bin-lock conflict resolved via build → start server → `dotnet test --no-build` |
| **Frontend tests** | `npm run test:frontend` (after each fix) | ✅ Passed | 96/96 × 2 (after F4 fix + final) |
| **Shared-header sweep (F1 retest)** | `/`, `/providers`, `/tools` @390+360 | ✅ Passed | 0 overlaps, 0 overflow, buttons visible, hamburger visible — F1 fix holds app-wide |
| **FileTree expand + filter** | conversation @1920 | ✅ Passed | Folder expand shows children; filter `.cs` → Program.cs only (logs/dirs hidden); clear restores tree |
| **File create** | conversation @1920 | ✅ Passed | `Create New File` modal → POST `/filesystem/file` 200 → tree refresh → editor auto-opens |
| **CodeEditor save + Disk Diff** | conversation @1920 | ✅ Passed | Dirty state enables Save → POST save 200 → **disk proof** (`D:\Temp\Calc\uitest-tmp.txt` content matched) → Save re-disables; Disk Diff rendered `-old/+new` rows; 0 console errors |
| **Git stage** | conversation @1920 | ✅ Passed | POST `/git/stage` 200 → file under "Staged Changes (1)" |
| **Git unstage** | conversation @1920 | ❌→✅ | **F3** (below) — retest: POST `/git/unstage` **200**, staged(0), git CLI confirms `?? uitest-tmp.txt` |
| **Branch select (unborn repo)** | git drawer | ❌→✅ | **F4** (below) — retest: select shows `main (current)`, selectedIndex 0 |
| **Branch switch / commit / push** | git drawer | ⏭ Skipped by plan | Would rewrite user's repo history (Calc repo, zero commits) — drawer buttons verified present (New Branch, Commit, Fetch/Pull/Push, Stash) |
| **Git drawer toggle paths** | header branch btn + Git btn + ✕ close | ✅ Passed | Branch button = drawer toggle by design (title "Click to toggle Git panel"); all 3 paths exercised |
| **Conversation switch** | sidebar → `Random` | ✅ Passed | URL → `/conversations/e8c28acd…`, messages render (37 msg nodes), composer present, 0 errors |
| **Provider switch modal** | conversation @1920 | ✅ Passed | "Switch AI Provider & History Replay" modal, centered (diff 0), fits, target list rendered, closed without switching |
| **Mobile spot (post-interactions)** | conversation @390+360 | ✅ Passed (see F5 note) | 0 real overlaps (clip-aware recheck), 0 overflow, mobile nav visible |
| **Text Readability raw check** | conversation @390 | ⚠️ False positive | **F5** (below) |
| **Setup Wizard** | — | ⏭ Not exercised | `isSetupCompleted: true` (same as run 2) |

---

## 🔧 Fixes (Before → After)

| ID | Check | Viewport | Before (failure) | After (fix) | Status |
|---|---|---|---|---|---|
| F3 | Git unstage 400 (`fatal: could not resolve 'HEAD'`) | 1920x1080 | Before _(image removed by user)_ | After _(image removed by user)_ | ✅ RESOLVED |
| F4 | Branch select blank on zero-commit repo | 1920x1080 | Before _(image removed by user)_ | After _(image removed by user)_ | ✅ RESOLVED |

**F3 — unstage fails (HTTP 400) on repos with no commits**
- **Repro:** stage any file in a fresh `git init` repo → click unstage (−) → `POST /api/v1/git/unstage` 400, body `{"code":"unstage_failed","message":"fatal: could not resolve 'HEAD'"}`.
- **Root cause:** `GitCliService.UnstageAsync` used `git restore --staged`, which requires a resolvable HEAD; unborn branch (Calc repo has 0 commits) fails.
- **Fix** (`src/AIAgentHub.Infrastructure/Git/GitCliService.cs`): on HEAD-resolution failure, fall back to `git rm --cached -- <files>` (or `rm --cached -r --ignore-unmatch .` for all), which works without HEAD.
- **Verification:** retest → 200 + git CLI `git diff --cached` empty; `GitCliServiceTests` 4/4; full unit suite 315/315.

**F4 — branch selector renders empty on unborn branch**
- **Repro:** open Git drawer on zero-commit repo → branch `<select>` is blank (value `main` has no matching `<option>`; `git branch` returns nothing on unborn HEAD).
- **Fix** (`studio/StudioGitPanel.tsx`): render fallback `<option>` for `status.currentBranch` when not present in `branches`.
- **Verification:** retest → `main (current)` selected; `npm run build` + 96/96 frontend tests.

---

## ❌ Unresolved

**F2 — Live provider switch loses conversation context** (backend, integration suite)
- **Test:** `LiveProviderChatIntegrationTests.LiveProvider_SwitchFromAntigravityToTargetProvider_RecallsContextAndMaintainsContinuity(targetProviderId: "copilot")`
- **Symptom:** after switching antigravity → copilot, assistant replies with first-turn greeting (`"I am ready. Please provide the task or th…"`) instead of recalling the number given pre-switch (`Assert.Contains "127414"`/`"899593"` not found).
- **Reproduced:** 2/2 runs (full suite + filtered re-run) — consistent, not flaky.
- **Assessment:** conversation-history carry across provider switch appears broken for this path. NOT part of the v0.3 changed surface (no chat/provider backend in 26628a5); possibly related to uncommitted `fix-provider-loading` WIP (stash). **Not fixed here** — needs product decision; flagged for follow-up.

**F5 — Raw Gate 2.6 flags 10 overlap pairs on conversation @390 → classified FALSE POSITIVE**
- **Flagged:** header/breadcrumb/provider-chip text × chat message `LI` (`"Tested initialization…"`, rect y −7..351).
- **Why not real:** the `LI` is scrolled-out content inside `.chat-messages` (overflow-clipped) and both sides sit in *different* sticky contexts (`.app-header` sticky z100 vs chat list) — the check's skip-list doesn't cover overflow clipping or multi-sticky pairs. Visual proof: screenshot _(image removed by user)_ shows clean, non-overlapping layout.
- **Clip-aware recheck:** 0 real overlaps @390, 0 @360, no overflow either. Layout is healthy; no fix applied.
- **Suggestion (skill-level):** extend Gate 2.6 skip rules with `overflow-clip ancestor` + `distinct sticky contexts` to eliminate this artifact class in future runs.

---

## 📸 Screenshots
- F3 Before/After, F4 Before/After, F5 visual proof: _(image evidence removed by user)_
- Run-2 previews (desktop/tablet/mobile): _(image evidence removed by user)_

## 🧹 Test hygiene
- `D:\Temp\Calc\uitest-tmp.txt` created → used for save/diff/stage tests → **deleted**; `git status` clean of test artifacts (pre-existing untracked logs/bin untouched).
- No commits, no branch/checkout/push operations performed.
- Watchdogs: integration + git test runs monitored (stall 300s / total 1500s kill thresholds); all spawned PIDs terminated at teardown.
