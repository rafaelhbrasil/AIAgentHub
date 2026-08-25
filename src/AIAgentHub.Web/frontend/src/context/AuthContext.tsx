import React, { createContext, useContext, useState, useEffect, useCallback } from 'react';
import { apiFetch, setUnauthorizedHandler } from '../services/apiClient';
import { SetupStatusResponse, AuthSessionResponse } from '../types/api';
import { getSafeReturnUrl } from '../utils/urlRouting';

interface AuthContextType {
  isSetupCompleted: boolean;
  canResetWithoutCode: boolean;
  isRecoveryModeEnabled: boolean;
  isAuthenticated: boolean;
  username: string;
  isLoading: boolean;
  checkAuthAndSetup: () => Promise<void>;
  login: (username: string, password?: string) => Promise<{ success: boolean; error?: string; status?: number }>;
  logout: () => Promise<void>;
  setIsSetupCompleted: (val: boolean) => void;
  setIsAuthenticated: (val: boolean) => void;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [isSetupCompleted, setIsSetupCompleted] = useState<boolean>(true);
  const [canResetWithoutCode, setCanResetWithoutCode] = useState<boolean>(false);
  const [isRecoveryModeEnabled, setIsRecoveryModeEnabled] = useState<boolean>(false);
  const [isAuthenticated, setIsAuthenticated] = useState<boolean>(false);
  const [username, setUsername] = useState<string>('admin');
  const [isLoading, setIsLoading] = useState<boolean>(true);

  const checkAuthAndSetup = useCallback(async () => {
    setIsLoading(true);
    try {
      const setupRes = await apiFetch<SetupStatusResponse>('/api/v1/auth/setup/status');
      if (setupRes.ok && setupRes.data) {
        setIsSetupCompleted(setupRes.data.isSetupCompleted);
        setCanResetWithoutCode(setupRes.data.canResetWithoutCode);
        setIsRecoveryModeEnabled(setupRes.data.isRecoveryModeEnabled);

        if (!setupRes.data.isSetupCompleted) {
          setIsAuthenticated(false);
          setIsLoading(false);
          return;
        }
      }

      const sessionRes = await apiFetch<AuthSessionResponse>('/api/v1/auth/session');
      if (sessionRes.ok && sessionRes.data && sessionRes.data.isAuthenticated) {
        setIsAuthenticated(true);
        setUsername(sessionRes.data.username || 'admin');
      } else {
        setIsAuthenticated(false);
      }
    } finally {
      setIsLoading(false);
    }
  }, []);

  const login = useCallback(async (user: string, pass?: string) => {
    const res = await apiFetch<{ username: string; message?: string }>('/api/v1/auth/login', {
      method: 'POST',
      body: { username: user, password: pass },
    });

    if (res.ok && res.data) {
      setIsAuthenticated(true);
      setUsername(res.data.username || user);
      return { success: true };
    }

    let errorMsg = 'Login failed.';
    if (res.status === 401) {
      errorMsg = res.data?.message || 'Invalid username or password.';
    } else if (res.status === 500) {
      errorMsg = res.data?.message || 'Internal server error occurred. Please check server logs.';
    } else if (res.status === 0 || !res.status) {
      errorMsg = res.error ? `Network error: ${res.error}` : 'Unable to connect to server.';
    } else {
      errorMsg = res.data?.message || res.error || `Server error (HTTP ${res.status}).`;
    }

    return { success: false, error: errorMsg, status: res.status };
  }, []);

  const logout = useCallback(async () => {
    await apiFetch('/api/v1/auth/logout', { method: 'POST' });
    setIsAuthenticated(false);
    window.history.replaceState({}, '', '/');
    window.dispatchEvent(new PopStateEvent('popstate'));
  }, []);

  useEffect(() => {
    setUnauthorizedHandler(() => {
      setIsAuthenticated(false);
      const currentPath = window.location.pathname + window.location.search;
      const safeUrl = getSafeReturnUrl(currentPath);
      if (safeUrl && safeUrl !== '/' && !window.location.search.includes('returnUrl=')) {
        window.history.replaceState({}, '', `/login?returnUrl=${encodeURIComponent(safeUrl)}`);
      }
    });
    return () => setUnauthorizedHandler(null);
  }, []);

  useEffect(() => {
    checkAuthAndSetup();
  }, [checkAuthAndSetup]);

  return (
    <AuthContext.Provider
      value={{
        isSetupCompleted,
        canResetWithoutCode,
        isRecoveryModeEnabled,
        isAuthenticated,
        username,
        isLoading,
        checkAuthAndSetup,
        login,
        logout,
        setIsSetupCompleted,
        setIsAuthenticated,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
};

export function useAuth(): AuthContextType {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}
