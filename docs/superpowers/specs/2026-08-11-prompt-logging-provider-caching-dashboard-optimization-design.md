# Design: Prompt Logging, Provider Caching & Dashboard Optimization

**Date:** 2026-08-11  
**Status:** Approved  
**Author:** opencode

---

## 1. Overview

Three related optimizations for AIAgentHub:
1. **Prompt Logging** - Debug-level logging of provider commands with prompt redaction
2. **Provider Page Cache-First Loading** - Load from sessionStorage, manual refresh only
3. **Dashboard Optimization** - Loading skeletons, data caching, lazy workspace details

---

## 2. Prompt Logging

### 2.1 Requirements
- Log metadata when prompt is sent to any provider
- Replace actual prompt text with `<<user_prompt>>` placeholder in command line
- Configurable: can be disabled via config or log level
- Default: enabled at Debug level

### 2.2 Design

**Config Section** (`appsettings.json`):
```json
{
  "PromptLogging": {
    "Enabled": true,
    "VerbosityLevel": "Debug"
  }
}
```

**Interface** (`IPromptLogger`):
```csharp
public interface IPromptLogger
{
    void LogPromptSent(string providerName, string modelName, string commandLine, int promptLength);
    bool IsEnabled { get; }
}
```

**Implementation** (`PromptLogger`):
- Injects `ILogger<PromptLogger>` and `IConfiguration`
- Checks `PromptLogging:Enabled` config before logging
- Replaces prompt content in command line with `<<user_prompt>>`
- Logs at configured verbosity level (default: Debug)
- Example output: `[Debug] Prompt sent to ClaudeCode (model: claude-sonnet-4-20250514) via: "claude --model claude-sonnet-4-20250514 '<<user_prompt>>'" length=142 chars`

**Integration Point:**
- `CliProviderBase.ExecuteAsync()` - log before spawning process
- Inject `IPromptLogger` via constructor

### 2.3 Files
- New: `src/AIAgentHub.Infrastructure/Providers/PromptLogger.cs`
- Modify: `src/AIAgentHub.Infrastructure/Providers/CliProviderBase.cs`
- Modify: `src/AIAgentHub.Web/DependencyInjection.cs`
- Modify: `appsettings.json`

---

## 3. Provider Page Cache-First Loading

### 3.1 Requirements
- Load provider data from cache on page mount
- Show stale data immediately, refresh per-provider on demand
- "Refresh All" button for manual full refresh
- Loading animation during Refresh All

### 3.2 Design

**Frontend Caching Strategy:**
- Cache key: `providers_cache` in `sessionStorage`
- Cache structure: `{ data: Provider[], timestamp: number }`
- TTL: 5 minutes (300,000 ms)
- On page load: check cache → if valid, render immediately
- If cache invalid/missing: show skeleton, fetch all, cache result

**Per-Provider Refresh:**
- Existing `refreshProvider(id)` function
- Fetches `/status?refresh=true` and `/models?refresh=true`
- Updates `state.providers` and `state.providerModels`
- Updates cache in sessionStorage

**Refresh All Button:**
- Triggers parallel refresh of all providers
- Shows page-wide loading overlay with animation
- Updates cache when complete

**Loading Skeleton:**
- CSS-only shimmer animation
- Matches provider card dimensions
- Shows 4 skeleton cards (typical provider count)

### 3.3 Files
- Modify: `wwwroot/js/app.js` - caching logic, skeleton rendering
- Modify: `wwwroot/css/app.css` - skeleton styles, loading overlay

---

## 4. Dashboard Optimization

### 4.1 Requirements
- Cache dashboard data (workspace list, provider count)
- Show loading skeletons on first load
- Lazy load workspace details
- Show "Last updated" indicator

### 4.2 Design

**Data Caching:**
- Cache key: `dashboard_cache` in `sessionStorage`
- Cache structure: `{ workspaces: Workspace[], providers: Provider[], timestamp: number }`
- TTL: 3 minutes (180,000 ms)
- On dashboard render: check cache → if valid, render from cache
- If invalid/missing: show skeletons, fetch data, cache result

**Loading Skeletons:**
- 3 stat card skeletons (workspaces, providers, port)
- 5 workspace row skeletons
- CSS shimmer animation matching card dimensions

**Lazy Workspace Details:**
- Initial render: workspace name + status badge only
- On scroll into viewport (IntersectionObserver): load file count, last modified
- Cache lazy-loaded details per workspace
- Fade-in animation when details load

**Last Updated Indicator:**
- Show timestamp below stat cards
- "Updated X seconds/minutes ago" format
- Refreshes when data is fetched

### 4.3 Files
- Modify: `wwwroot/js/app.js` - caching, lazy loading, skeleton rendering
- Modify: `wwwroot/css/app.css` - skeleton styles, lazy load animations

---

## 5. Performance Benchmarks

### 5.1 What to Measure
- Dashboard render time (first paint to interactive)
- Provider page render time
- API response times
- Subprocess detection time per provider

### 5.2 Implementation
- Frontend: `performance.mark()` / `performance.measure()` in key functions
- Log results to console when `BENCHMARKS_ENABLED` config is true
- Simple benchmark script in docs for manual testing

### 5.3 Files
- Modify: `wwwroot/js/app.js` - add performance marks
- New: `docs/benchmarks.md` - manual testing guide

---

## 6. Testing Strategy

### 6.1 Unit Tests
- `PromptLogger` - config toggles, log output format
- Provider caching - cache hit/miss, TTL expiration
- Dashboard caching - cache hit/miss, TTL expiration

### 6.2 Playwright Tests
- Dashboard loading skeletons appear
- Provider page cache-first behavior
- Refresh All button triggers loading animation
- Lazy loading of workspace details

---

## 7. Rollback

All changes are additive:
- Prompt logging: disable via `PromptLogging:Enabled = false`
- Caching: clear sessionStorage or disable in code
- Skeletons: CSS-only, no functional impact
