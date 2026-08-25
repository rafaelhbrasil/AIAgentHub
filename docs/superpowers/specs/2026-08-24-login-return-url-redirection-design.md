# Login Return URL Redirection and Deep-Link Preservation Design

## Overview
This specification defines the authentication redirection, deep-link preservation, and safe return URL validation mechanism for AI Agent Hub.

When users visit protected URLs (such as `/settings`, `/providers`, `/tools`, or deep links to specific workspaces and conversations like `/workspaces/{wsId}/conversations/{convId}`) while unauthenticated or after their session expires, the application preserves the intended destination in the URL query string (`?returnUrl=...`) and safely redirects back to that destination upon successful authentication.

---

## Functional Requirements

### 1. Return URL Capture on Protected Navigation & Session Expiry
- When an unauthenticated user navigates directly to a protected page (e.g., `/settings` or `/workspaces/ws1/conversations/conv2`), the application displays the login page while preserving the destination path.
- When an active session expires or an API request receives a `401 Unauthorized` response on a protected endpoint, the frontend updates the route to `/login?returnUrl=${encodeURIComponent(currentPath)}`.
- If the current path is the root `/` or already `/login`, no `returnUrl` parameter is appended.

### 2. Open-Redirect Prevention & Safe URL Validation
To prevent open-redirect vulnerabilities (where malicious actors construct links like `https://hub.local/login?returnUrl=https://evil.com` or `//evil.com`), all return URLs MUST be validated using a dedicated `getSafeReturnUrl(rawUrl)` utility before performing any browser redirection:
- **Relative Path Requirement**: The URL must start with a forward slash `/`.
- **Protocol-Relative Exploit Block**: The URL must NOT start with `//`.
- **Backslash & Control Character Sanitization**: The URL must NOT contain backslashes `\` or encoded backslashes `%5C` that could be parsed as host separators by browsers.
- **Scheme Check**: The URL must NOT start with or contain URI schemes (`http:`, `https:`, `javascript:`, `data:`, `vbscript:`).
- **Self-Redirect Loop Prevention**: The URL must NOT redirect back to `/login` or `/login?...`.
- **Fallback**: If validation fails, `getSafeReturnUrl` returns `null` (or `/`), falling back to the default dashboard.

### 3. Post-Authentication Redirection Workflow
Upon successful login via `SignInPage.tsx`:
1. The component checks `window.location.search` for a `returnUrl` query parameter. If absent, it checks `window.location.pathname`.
2. Passes the value through `getSafeReturnUrl(returnUrl)`.
3. If a valid safe return URL is resolved:
   - Updates the browser history using `window.history.replaceState({}, '', safeUrl)`.
   - Triggers route parsing so the active tab, workspace ID, and conversation ID update accordingly.
4. If no valid return URL is found or if it resolves to root, navigation defaults to `/` (dashboard).

---

## Architecture & Component Updates

### 1. `src/AIAgentHub.Web/frontend/src/utils/urlRouting.ts`
- Implement `getSafeReturnUrl(rawUrl?: string | null): string | null`.
- Update `parseUrlPath(pathname: string)` to handle `/login` route gracefully without crashing or creating invalid state.

### 2. `src/AIAgentHub.Web/frontend/src/components/auth/SignInPage.tsx`
- On login form submission, retrieve and sanitize the `returnUrl`.
- On successful login response, update history and notify route listeners.

### 3. `src/AIAgentHub.Web/frontend/src/services/apiClient.ts` & `AuthContext.tsx`
- In `apiClient.ts`, support 401 callback / unauthorized handling to trigger redirection to `/login?returnUrl=...`.

---

## Verification Plan

### Automated Unit Tests (`src/AIAgentHub.Web/frontend/tests/urlRouting.test.ts`)
- Verify valid relative paths (`/settings`, `/workspaces/123`, `/workspaces/123/conversations/456?filter=true`) return the exact sanitized relative path.
- Verify absolute URLs (`https://evil.com`, `http://evil.com`) return `null`.
- Verify protocol-relative URLs (`//evil.com`, `///evil.com`) return `null`.
- Verify backslash evasion (`/\evil.com`, `/%5cevil.com`) return `null`.
- Verify javascript pseudo-protocols (`javascript:alert(1)`) return `null`.
- Verify login loops (`/login`, `/login?returnUrl=/login`) return `null`.

### Frontend & Integration Verification
- Execute `npm test` to ensure all 50+ frontend unit tests and 155+ backend unit tests pass.
