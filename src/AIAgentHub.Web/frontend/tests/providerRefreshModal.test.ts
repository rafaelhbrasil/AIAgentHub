import { describe, it, expect } from 'vitest';
import { ProviderStatus } from '../src/types/provider';
import {
  isReadyStatus,
  isUnauthenticatedStatus,
  isNotInstalledStatus,
  isQuotaExceededStatus,
  isDiscontinuedStatus,
} from '../src/utils/providerSort';

describe('ProviderRefreshModal state helpers', () => {
  it('correctly categorizes provider status types for badge display', () => {
    expect(isReadyStatus(ProviderStatus.Ready)).toBe(true);
    expect(isUnauthenticatedStatus(ProviderStatus.Unauthenticated)).toBe(true);
    expect(isNotInstalledStatus(ProviderStatus.NotInstalled)).toBe(true);
    expect(isQuotaExceededStatus(ProviderStatus.QuotaExceeded)).toBe(true);
    expect(isDiscontinuedStatus(ProviderStatus.Discontinued)).toBe(true);
  });

  it('calculates progress percentage correctly', () => {
    const totalInstalled = 4;
    const completedCount = 3;
    const percentage = Math.round((completedCount / totalInstalled) * 100);
    expect(percentage).toBe(75);
  });

  it('handles 0 installed providers without division by zero', () => {
    const totalInstalled = 0;
    const completedCount = 0;
    const percentage = totalInstalled > 0 ? Math.round((completedCount / totalInstalled) * 100) : 100;
    expect(percentage).toBe(100);
  });
});
