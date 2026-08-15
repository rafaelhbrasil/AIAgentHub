import { ProviderDto } from '../types/provider';

export function getProviderSortPriority(p: ProviderDto): number {
  if (p.id === 'gemini' || (p.message && p.message.toLowerCase().includes('discontinued'))) return 99;
  if (p.status === 'Ready' || p.status === 2) return 1;
  if (p.status === 'Unauthenticated' || p.status === 1) return 2;
  if (p.status === 'NotInstalled' || p.status === 0) return 3;
  if (p.status === 'QuotaExceeded' || p.status === 5) return 4;
  return 5;
}

export function sortProviders(providers: ProviderDto[]): ProviderDto[] {
  return [...providers].sort((a, b) => getProviderSortPriority(a) - getProviderSortPriority(b));
}
