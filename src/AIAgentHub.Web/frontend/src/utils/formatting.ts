import { ModelInfo } from '../types/provider';

export function formatModelsSummary(models?: ModelInfo[]): string {
  if (!models || models.length === 0) return 'no models available';
  const activeCount = models.filter((m) => m.isDisplayed !== false).length;
  return `${models.length} model${models.length === 1 ? '' : 's'} available (${activeCount} active)`;
}

export function formatFileSize(bytes?: number): string {
  if (bytes === undefined || bytes === null) return '';
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  return `${(bytes / 1e9).toFixed(1)} GB`;
}

export function formatTime(isoString?: string): string {
  if (!isoString) return '';
  try {
    return new Date(isoString).toLocaleTimeString();
  } catch {
    return '';
  }
}

export function formatAppVersion(version?: string): string {
  if (!version) return '';
  const clean = version.startsWith('v') || version.startsWith('V') ? version.slice(1) : version;
  const trimmed = clean.replace(/^(\d+\.\d+\.\d+)\.0$/, '$1');
  return `v${trimmed}`;
}

