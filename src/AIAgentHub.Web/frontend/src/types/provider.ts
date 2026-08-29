export enum ProviderStatus {
  NotInstalled = 'NotInstalled',
  Unauthenticated = 'Unauthenticated',
  Ready = 'Ready',
  Error = 'Error',
  Running = 'Running',
  QuotaExceeded = 'QuotaExceeded',
  Discontinued = 'Discontinued',
}

export type ProviderStatusType = ProviderStatus | string;

export interface ModelInfo {
  id: string;
  displayName?: string;
  contextWindow?: number;
  isDefault?: boolean;
  isDisplayed?: boolean;
}

export interface ProviderDto {
  id: string;
  displayName: string;
  description: string;
  isInstalled: boolean;
  status: ProviderStatusType;
  message?: string;
  capabilities: number;
  supportedModels: ModelInfo[];
  documentationUrl?: string;
  installCommand?: string;
  installInstructions?: string;
  quotaResetsAt?: string;
  isHidden?: boolean;
  defaultModelId?: string | null;
  defaultEffort?: string | null;
}

export interface ProviderStatusDto {
  providerId: string;
  status: ProviderStatusType;
  message?: string;
  quotaResetsAt?: string;
  documentationUrl?: string;
}
