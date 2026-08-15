export interface NetworkInterfaceDto {
  name: string;
  ipAddress: string;
  status: string;
}

export interface ServerSettingsDto {
  id: string;
  networkMode: number; // 0=Localhost, 1=LAN, 2=SelectedInterfaces
  listeningPortHttps: number;
  listeningPortHttp: number;
  selectedInterfaces?: string[];
  theme?: string;
  isSetupCompleted?: boolean;
  createdAtUtc?: string;
  updatedAtUtc?: string;
}

export interface UpdateServerSettingsRequest {
  networkMode: number;
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
