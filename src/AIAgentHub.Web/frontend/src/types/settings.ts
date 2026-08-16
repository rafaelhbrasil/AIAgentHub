export enum NetworkMode {
  Localhost = 'Localhost',
  Lan = 'Lan',
  SelectedInterfaces = 'SelectedInterfaces',
}

export type NetworkModeType = NetworkMode | string;

export function normalizeNetworkMode(mode: unknown): NetworkMode {
  if (mode === NetworkMode.Lan) return NetworkMode.Lan;
  if (mode === NetworkMode.SelectedInterfaces) return NetworkMode.SelectedInterfaces;
  return NetworkMode.Localhost;
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
