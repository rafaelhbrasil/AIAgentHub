import { describe, it, expect } from 'vitest';
import { formatModelsSummary, formatFileSize, formatTime } from '../src/utils/formatting';
import { ModelInfo } from '../src/types/provider';
import { isUserRole } from '../src/types/conversation';
import { normalizeNetworkMode } from '../src/types/settings';

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

  it('correctly identifies user role from both string and numeric enum values', () => {
    expect(isUserRole(0)).toBe(true);
    expect(isUserRole('User')).toBe(true);
    expect(isUserRole('user')).toBe(true);
    expect(isUserRole('USER')).toBe(true);
    expect(isUserRole('Assistant')).toBe(false);
    expect(isUserRole('assistant')).toBe(false);
    expect(isUserRole(1)).toBe(false);
    expect(isUserRole(null)).toBe(false);
    expect(isUserRole(undefined)).toBe(false);
  });

  it('normalizes network mode values from both string enum and numeric values', () => {
    expect(normalizeNetworkMode('Lan')).toBe('Lan');
    expect(normalizeNetworkMode('lan')).toBe('Lan');
    expect(normalizeNetworkMode(1)).toBe('Lan');
    expect(normalizeNetworkMode('1')).toBe('Lan');

    expect(normalizeNetworkMode('SelectedInterfaces')).toBe('SelectedInterfaces');
    expect(normalizeNetworkMode('selectedInterfaces')).toBe('SelectedInterfaces');
    expect(normalizeNetworkMode(2)).toBe('SelectedInterfaces');

    expect(normalizeNetworkMode('Localhost')).toBe('Localhost');
    expect(normalizeNetworkMode(0)).toBe('Localhost');
    expect(normalizeNetworkMode(null)).toBe('Localhost');
    expect(normalizeNetworkMode(undefined)).toBe('Localhost');
  });
});
