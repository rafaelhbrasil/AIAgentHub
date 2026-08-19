# Specification: Conversation URL Deep Linking & SPA Routing

**Date:** 2026-08-19  
**Status:** Approved  
**Scope:** Frontend Navigation, Deep Linking & URL State Management

---

## 1. Overview

Users need the ability to view, share, copy, and bookmark specific conversations directly via the browser address bar URL.

---

## 2. SPA URL Routing Structure

The Single Page Application (SPA) supports the following hierarchical URL patterns:

| Route Path | View / Destination | Context / State |
|---|---|---|
| `/` or `/dashboard` | Dashboard View | Managed workspaces overview, providers status |
| `/workspaces` | Workspaces List | Grid of managed workspaces |
| `/workspaces/:workspaceId` | Workspace Studio | Workspace opened, automatically loads first available conversation |
| `/workspaces/:workspaceId/conversations/:conversationId` | Workspace Studio | Workspace opened with direct deep-link selection of `:conversationId` |
| `/workspaces/:workspaceId/:conversationId` | Workspace Studio | Alternative compact deep-link to `:conversationId` |
| `/providers` | AI Providers View | List of providers, models, status |
| `/tools` or `/mcps` | MCPs & Skills View | MCP registry and installed skills |
| `/settings` | Settings View | Network interfaces, TLS, security |

---

## 3. Dynamic State Synchronization

1. **Selection & Switching**:
   - Selecting a conversation in the sidebar or creating a new conversation automatically pushes `/workspaces/:workspaceId/conversations/:conversationId` to browser history (`history.pushState`).
2. **Direct Access & Reload**:
   - Navigating directly to `/workspaces/:workspaceId/conversations/:conversationId` resolves the workspace and immediately selects and joins the specified conversation.
3. **Deletion & Fallbacks**:
   - Deleting the active conversation automatically transitions the URL to the newly active conversation or `/workspaces/:workspaceId` if no conversations remain.
4. **Navigation Back**:
   - Clicking "Back to Workspaces" transitions the URL to `/workspaces`.
