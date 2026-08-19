import { NavTab } from '../components/common/Header';

export interface RouteState {
  tab: NavTab;
  workspaceId: string | null;
  conversationId: string | null;
}

export function parseUrlPath(pathname: string): RouteState {
  const parts = pathname.split('/').filter(Boolean);

  if (parts.length === 0 || parts[0] === 'dashboard') {
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
