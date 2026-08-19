import { describe, it, expect } from 'vitest';
import { parseUrlPath } from '../src/utils/urlRouting';

describe('urlRouting parser', () => {
  it('parses root and dashboard paths', () => {
    expect(parseUrlPath('/')).toEqual({ tab: 'dashboard', workspaceId: null, conversationId: null });
    expect(parseUrlPath('/dashboard')).toEqual({ tab: 'dashboard', workspaceId: null, conversationId: null });
  });

  it('parses workspaces list path', () => {
    expect(parseUrlPath('/workspaces')).toEqual({ tab: 'workspaces', workspaceId: null, conversationId: null });
  });

  it('parses workspace studio path without conversation', () => {
    expect(parseUrlPath('/workspaces/ws-uuid-123')).toEqual({
      tab: 'workspaces',
      workspaceId: 'ws-uuid-123',
      conversationId: null,
    });
  });

  it('parses canonical conversation deep link path', () => {
    expect(parseUrlPath('/workspaces/ws-uuid-123/conversations/conv-uuid-456')).toEqual({
      tab: 'workspaces',
      workspaceId: 'ws-uuid-123',
      conversationId: 'conv-uuid-456',
    });
  });

  it('parses compact conversation path', () => {
    expect(parseUrlPath('/workspaces/ws-uuid-123/conv-uuid-456')).toEqual({
      tab: 'workspaces',
      workspaceId: 'ws-uuid-123',
      conversationId: 'conv-uuid-456',
    });
  });

  it('parses providers, tools, and settings paths', () => {
    expect(parseUrlPath('/providers')).toEqual({ tab: 'providers', workspaceId: null, conversationId: null });
    expect(parseUrlPath('/tools')).toEqual({ tab: 'tools', workspaceId: null, conversationId: null });
    expect(parseUrlPath('/mcps')).toEqual({ tab: 'tools', workspaceId: null, conversationId: null });
    expect(parseUrlPath('/settings')).toEqual({ tab: 'settings', workspaceId: null, conversationId: null });
  });

  it('falls back to dashboard for unknown routes', () => {
    expect(parseUrlPath('/unknown-path')).toEqual({ tab: 'dashboard', workspaceId: null, conversationId: null });
  });
});
