# 🧪 UI Smoke Test Report

- **Date & Time:** 2026-10-05 23:52:00 (UTC+1)
- **User:** Rafael Brasil
- **Target URL:** `http://localhost:5433/` (Login Page in `/` unauthenticated)
- **Mode:** `Default (Happy Path & Dialogs)`
- **Overall Status:** ✅ Passed

---

## 📊 Smoke Test Scorecard

| Gate / Check | Scope | Result | Details |
|---|---|---|---|
| **Server Lifecycle** | `launchSettings.json` (5432/5433) | ✅ Healthy | Spawned dotnet instance (PIDs 44652, 40884) cleanly terminated; zero locked processes. |
| **Network Integrity** | Static assets & API calls | ✅ Passed | 15+ network requests, 0 static 4xx/5xx failures (HTTP 200 on all assets and bootstrap APIs). |
| **Console Hygiene** | JavaScript runtime | ✅ Clean | 0 uncaught exceptions, 0 unhandled promise rejections; clean SignalR lifecycle logs. |
| **Desktop Viewport** | 1920x1080 | ✅ Passed | `scrollWidth <= innerWidth` (No horizontal overflow). |
| **Mobile Viewport** | 390x844 | ✅ Passed | `scrollWidth <= innerWidth` (Clean responsive single-column card layout). |
| **Critical Scenarios** | Happy path & dialogs | ✅ Passed | Tested `Lost password? Enter Recovery Code` modal (opened cleanly, validated fields, closed via dismiss button). Autocomplete verified on login form. Valid login authenticated and redirected to Dashboard. |
| **Negative Tests** | Form validation & wrong auth | ✅ Passed | Empty username triggered client-side toast `Username is required.`. Wrong password triggered server rate-limited warning toast without 500 error or page freeze. |

---

## 🔍 Detailed Observations

1. **Browser Autofill & Autocomplete Verification**:
   - The login form inputs (`#loginUsername` and `#loginPassword`) properly presented `name="username"`, `autoComplete="username"`, `name="password"`, and `autoComplete="current-password"`.
   - The browser automatically populated credentials without requiring manual typing.
2. **Same-Page Recovery Modal**:
   - Clicking `"Lost password? Enter Recovery Code"` dynamically opened `RecoveryModal` overlay.
   - Textbox for 16-character recovery code rendered with clean instructions.
   - Modal dismiss via `Close modal` (`×`) operated smoothly and restored the login view.
3. **Session Lifecycle**:
   - Authenticated user login via `admin` credentials successfully established the session and navigated to the Dashboard view.
