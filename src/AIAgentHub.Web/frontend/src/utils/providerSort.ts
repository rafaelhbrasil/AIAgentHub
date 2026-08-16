import { ProviderDto, ProviderStatus } from '../types/provider';

export const isDiscontinuedStatus = (status: unknown): boolean => status === ProviderStatus.Discontinued;
export const isReadyStatus = (status: unknown): boolean => status === ProviderStatus.Ready;
export const isUnauthenticatedStatus = (status: unknown): boolean => status === ProviderStatus.Unauthenticated;
export const isNotInstalledStatus = (status: unknown): boolean => status === ProviderStatus.NotInstalled;
export const isQuotaExceededStatus = (status: unknown): boolean => status === ProviderStatus.QuotaExceeded;

export function isProviderOperational(p: ProviderDto): boolean {
  return isReadyStatus(p.status) && !isDiscontinuedStatus(p.status);
}

export function getProviderSortPriority(p: ProviderDto): number {
  if (isDiscontinuedStatus(p.status)) return 99;
  if (isReadyStatus(p.status)) return 1;
  if (isUnauthenticatedStatus(p.status)) return 2;
  if (isNotInstalledStatus(p.status)) return 3;
  if (isQuotaExceededStatus(p.status)) return 4;
  return 5;
}

export function sortProviders(providers: ProviderDto[]): ProviderDto[] {
  return [...providers].sort((a, b) => getProviderSortPriority(a) - getProviderSortPriority(b));
}
