export type ProviderStatusType =
  | 'NotInstalled'
  | 'Unauthenticated'
  | 'Ready'
  | 'Error'
  | 'Running'
  | 'QuotaExceeded'
  | number;

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
}

export interface ProviderStatusDto {
  providerId: string;
  status: ProviderStatusType;
  message?: string;
  quotaResetsAt?: string;
  documentationUrl?: string;
}
