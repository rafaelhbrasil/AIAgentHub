import { NavTab } from '../components/common/Header';

export interface RouteState {
  tab: NavTab;
  workspaceId: string | null;
  conversationId: string | null;
}

/**
 * Validates and sanitizes a return URL to prevent open-redirect vulnerabilities.
 * Ensures the target is a relative path within the application and not a protocol-relative URL,
 * an absolute URL, a scheme-based exploit, or a self-redirect loop to /login.
 */
export function getSafeReturnUrl(rawUrl?: string | null): string | null {
  if (!rawUrl || typeof rawUrl !== 'string') {
    return null;
  }

  // Reject control characters or newlines
  if (/[\r\n\t]/.test(rawUrl)) {
    return null;
  }

  const trimmed = rawUrl.trim();

  // Must start with exactly one forward slash, not '//' (protocol-relative) and not containing backslashes
  if (!trimmed.startsWith('/') || trimmed.startsWith('//') || trimmed.includes('\\')) {
    return null;
  }

  // Reject encoded backslashes
  if (/%5c/i.test(trimmed)) {
    return null;
  }

  // Reject URIs with schemes (e.g. javascript:, data:, http:, https:)
  if (/^[a-zA-Z][a-zA-Z0-9+.-]*:/.test(trimmed)) {
    return null;
  }

  // Prevent redirect loops to /login
  const pathWithoutQuery = trimmed.split('?')[0].split('#')[0].toLowerCase();
  if (pathWithoutQuery === '/login' || pathWithoutQuery === '/login/') {
    return null;
  }

  return trimmed;
}

export function parseUrlPath(pathname: string): RouteState {
  const parts = pathname.split('/').filter(Boolean);

  if (parts.length === 0 || parts[0] === 'dashboard' || parts[0] === 'login') {
    return { tab: 'dashboard', workspaceId: null, conversationId: null };
  } else if (parts[0] === 'workspaces') {
    const workspaceId = parts.length >= 2 ? parts[1] : null;
    let conversationId: string | null = null;
    if (parts.length >= 4 && parts[2] === 'conversations') {
      conversationId = parts[3];
    } else if (parts.length >= 3 && parts[2] !== 'conversations') {
      conversationId = parts[2];
    }
    return { tab: 'workspaces', workspaceId, conversationId };
  } else if (parts[0] === 'providers') {
    return { tab: 'providers', workspaceId: null, conversationId: null };
  } else if (parts[0] === 'tools' || parts[0] === 'mcps') {
    return { tab: 'tools', workspaceId: null, conversationId: null };
  } else if (parts[0] === 'settings') {
    return { tab: 'settings', workspaceId: null, conversationId: null };
  }
  return { tab: 'dashboard', workspaceId: null, conversationId: null };
}

export function parseUrlRoute(): RouteState {
  if (typeof window === 'undefined') {
    return { tab: 'dashboard', workspaceId: null, conversationId: null };
  }
  return parseUrlPath(window.location.pathname);
}
