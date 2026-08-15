import { describe, it, expect } from 'vitest';
import { getProviderSortPriority, sortProviders } from '../src/utils/providerSort';
import { ProviderDto } from '../src/types/provider';

describe('providerSort utils', () => {
  const readyProv: ProviderDto = {
    id: 'antigravity',
    displayName: 'Antigravity CLI',
    description: 'Operational provider',
    isInstalled: true,
    status: 'Ready',
    capabilities: 1,
    supportedModels: [],
  };

  const unauthProv: ProviderDto = {
    id: 'claude',
    displayName: 'Claude Code',
    description: 'Requires auth',
    isInstalled: true,
    status: 'Unauthenticated',
    capabilities: 1,
    supportedModels: [],
  };

  const notInstalledProv: ProviderDto = {
    id: 'opencode',
    displayName: 'OpenCode',
    description: 'Not installed',
    isInstalled: false,
    status: 'NotInstalled',
    capabilities: 1,
    supportedModels: [],
  };

  const discontinuedProv: ProviderDto = {
    id: 'gemini',
    displayName: 'Gemini CLI',
    description: 'Discontinued CLI',
    isInstalled: true,
    status: 'Ready',
    message: 'CLI has been discontinued',
    capabilities: 1,
    supportedModels: [],
  };

  it('assigns correct sort priority based on provider status', () => {
    expect(getProviderSortPriority(readyProv)).toBe(1);
    expect(getProviderSortPriority(unauthProv)).toBe(2);
    expect(getProviderSortPriority(notInstalledProv)).toBe(3);
    expect(getProviderSortPriority(discontinuedProv)).toBe(99);
  });

  it('sorts providers with Ready first, Unauthenticated second, NotInstalled third, and Discontinued last', () => {
    const list = [discontinuedProv, notInstalledProv, unauthProv, readyProv];
    const sorted = sortProviders(list);

    expect(sorted.map((p) => p.id)).toEqual(['antigravity', 'claude', 'opencode', 'gemini']);
  });
});
