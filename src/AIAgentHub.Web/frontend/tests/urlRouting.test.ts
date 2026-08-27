import { describe, it, expect } from 'vitest';
import { parseUrlPath, getSafeReturnUrl } from '../src/utils/urlRouting';

describe('urlRouting parser', () => {
  it('parses root and dashboard paths', () => {
    expect(parseUrlPath('/')).toEqual({ tab: 'dashboard', workspaceId: null, conversationId: null });
    expect(parseUrlPath('/dashboard')).toEqual({ tab: 'dashboard', workspaceId: null, conversationId: null });
  });

  it('parses login path to default state', () => {
    expect(parseUrlPath('/login')).toEqual({ tab: 'dashboard', workspaceId: null, conversationId: null });
    expect(parseUrlPath('/login/')).toEqual({ tab: 'dashboard', workspaceId: null, conversationId: null });
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

  it('strips query strings and hash fragments from parsed paths', () => {
    expect(parseUrlPath('/workspaces/ws-1?foo=bar#section')).toEqual({
      tab: 'workspaces',
      workspaceId: 'ws-1',
      conversationId: null,
    });
    expect(parseUrlPath('/settings?saved=true')).toEqual({
      tab: 'settings',
      workspaceId: null,
      conversationId: null,
    });
  });
});

describe('getSafeReturnUrl sanitizer', () => {
  it('accepts valid relative local paths', () => {
    expect(getSafeReturnUrl('/settings')).toBe('/settings');
    expect(getSafeReturnUrl('/providers')).toBe('/providers');
    expect(getSafeReturnUrl('/workspaces/ws-1')).toBe('/workspaces/ws-1');
    expect(getSafeReturnUrl('/workspaces/ws-1/conversations/conv-2')).toBe('/workspaces/ws-1/conversations/conv-2');
    expect(getSafeReturnUrl('/workspaces/ws-1/conversations/conv-2?query=1#top')).toBe('/workspaces/ws-1/conversations/conv-2?query=1#top');
  });

  it('rejects absolute URLs with schemes', () => {
    expect(getSafeReturnUrl('https://evil.com')).toBeNull();
    expect(getSafeReturnUrl('http://evil.com/settings')).toBeNull();
    expect(getSafeReturnUrl('javascript:alert(1)')).toBeNull();
    expect(getSafeReturnUrl('data:text/html,evil')).toBeNull();
  });

  it('rejects protocol-relative URLs (//evil.com)', () => {
    expect(getSafeReturnUrl('//evil.com')).toBeNull();
    expect(getSafeReturnUrl('//evil.com/settings')).toBeNull();
    expect(getSafeReturnUrl('///evil.com')).toBeNull();
  });

  it('rejects backslash evasion and encoded characters', () => {
    expect(getSafeReturnUrl('/\\evil.com')).toBeNull();
    expect(getSafeReturnUrl('/path\\with\\backslash')).toBeNull();
    expect(getSafeReturnUrl('/%5cevil.com')).toBeNull();
    expect(getSafeReturnUrl('/%5Cevil.com')).toBeNull();
    expect(getSafeReturnUrl('/settings\n')).toBeNull();
  });

  it('rejects self-redirect loops to /login', () => {
    expect(getSafeReturnUrl('/login')).toBeNull();
    expect(getSafeReturnUrl('/login/')).toBeNull();
    expect(getSafeReturnUrl('/login?returnUrl=/settings')).toBeNull();
  });

  it('rejects null, undefined, empty, or non-string inputs', () => {
    expect(getSafeReturnUrl(null)).toBeNull();
    expect(getSafeReturnUrl(undefined)).toBeNull();
    expect(getSafeReturnUrl('')).toBeNull();
    expect(getSafeReturnUrl('   ')).toBeNull();
  });
});
