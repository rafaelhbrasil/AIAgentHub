import { describe, it, expect } from 'vitest';
import { formatModelsSummary, formatFileSize, formatTime } from '../src/utils/formatting';
import { ModelInfo } from '../src/types/provider';
import { MessageRole, isUserRole } from '../src/types/conversation';
import { NetworkMode, normalizeNetworkMode } from '../src/types/settings';

describe('formatting utils', () => {
  it('formats models summary correctly with active count', () => {
    const models: ModelInfo[] = [
      { id: 'm1', displayName: 'Model 1', isDisplayed: true },
      { id: 'm2', displayName: 'Model 2', isDisplayed: false },
      { id: 'm3', displayName: 'Model 3', isDisplayed: true },
    ];

    const summary = formatModelsSummary(models);
    expect(summary).toBe('3 models available (2 active)');
  });

  it('formats empty models array', () => {
    expect(formatModelsSummary([])).toBe('no models available');
    expect(formatModelsSummary(undefined)).toBe('no models available');
  });

  it('formats byte sizes to readable strings', () => {
    expect(formatFileSize(500)).toBe('500 B');
    expect(formatFileSize(2048)).toBe('2.0 KB');
    expect(formatFileSize(1048576 * 5)).toBe('5.0 MB');
    expect(formatFileSize(1e10)).toBe('10.0 GB');
  });

  it('formats valid ISO time strings and handles invalid gracefully', () => {
    const valid = '2026-08-14T12:30:00Z';
    expect(formatTime(valid)).not.toBe('');
    expect(formatTime('')).toBe('');
  });

  it('correctly identifies user role from string and enum values', () => {
    expect(isUserRole(MessageRole.User)).toBe(true);
    expect(isUserRole('User')).toBe(true);
    expect(isUserRole(MessageRole.Assistant)).toBe(false);
    expect(isUserRole('Assistant')).toBe(false);
    expect(isUserRole(null)).toBe(false);
    expect(isUserRole(undefined)).toBe(false);
  });

  it('normalizes network mode values from string enum values', () => {
    expect(normalizeNetworkMode(NetworkMode.Lan)).toBe(NetworkMode.Lan);
    expect(normalizeNetworkMode('Lan')).toBe(NetworkMode.Lan);

    expect(normalizeNetworkMode(NetworkMode.SelectedInterfaces)).toBe(NetworkMode.SelectedInterfaces);
    expect(normalizeNetworkMode('SelectedInterfaces')).toBe(NetworkMode.SelectedInterfaces);

    expect(normalizeNetworkMode(NetworkMode.Localhost)).toBe(NetworkMode.Localhost);
    expect(normalizeNetworkMode('Localhost')).toBe(NetworkMode.Localhost);
    expect(normalizeNetworkMode(null)).toBe(NetworkMode.Localhost);
    expect(normalizeNetworkMode(undefined)).toBe(NetworkMode.Localhost);
  });
});
