export type NetworkModeType = 'Localhost' | 'Lan' | 'SelectedInterfaces' | 0 | 1 | 2;

export function normalizeNetworkMode(mode: unknown): 'Localhost' | 'Lan' | 'SelectedInterfaces' {
  if (mode === 1 || mode === '1' || mode === 'Lan' || mode === 'lan') return 'Lan';
  if (mode === 2 || mode === '2' || mode === 'SelectedInterfaces' || mode === 'selectedInterfaces') return 'SelectedInterfaces';
  return 'Localhost';
}

export interface NetworkInterfaceDto {
  name: string;
  ipAddress: string;
  status: string;
}

export interface ServerSettingsDto {
  id: string;
  networkMode: NetworkModeType;
  listeningPortHttps: number;
  listeningPortHttp: number;
  selectedInterfaces?: string[];
  theme?: string;
  isSetupCompleted?: boolean;
  createdAtUtc?: string;
  updatedAtUtc?: string;
}

export interface UpdateServerSettingsRequest {
  networkMode: NetworkModeType;
  listeningPortHttps?: number;
  listeningPortHttp?: number;
  selectedInterfaces?: string[];
  theme?: string;
}

export interface McpDto {
  id: string;
  name: string;
  description?: string;
  isEnabled: boolean;
}

export interface SkillDto {
  id: string;
  name: string;
  description?: string;
  isEnabled: boolean;
}

export interface PermissionRequestDto {
  id: string;
  providerId: string;
  type: string;
  target: string;
  reason: string;
}
