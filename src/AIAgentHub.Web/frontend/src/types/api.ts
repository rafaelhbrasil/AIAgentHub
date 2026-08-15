export interface ApiResponse<T = any> {
  ok: boolean;
  status: number;
  data?: T;
  error?: string;
}

export interface SetupStatusResponse {
  isSetupCompleted: boolean;
  canResetWithoutCode: boolean;
  isRecoveryModeEnabled: boolean;
}

export interface AuthSessionResponse {
  isAuthenticated: boolean;
  username?: string;
  role?: string;
}

export interface InitializeSetupResponse {
  username: string;
  recoveryCode: string;
  isSetupCompleted: boolean;
}
