import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { act } from 'react';
import { createRoot, Root } from 'react-dom/client';
import { renderToString } from 'react-dom/server';
import { SettingsView } from '../src/components/settings/SettingsView';
import { ServerSettingsDto, NetworkInterfaceDto, NetworkMode } from '../src/types/settings';
import { ToastProvider } from '../src/context/ToastContext';
import { AuthProvider } from '../src/context/AuthContext';
import * as apiClient from '../src/services/apiClient';

describe('SettingsView UI Component', () => {
  let container: HTMLDivElement;
  let root: Root | null = null;

  beforeEach(() => {
    vi.restoreAllMocks();
    container = document.createElement('div');
    document.body.appendChild(container);
  });

  afterEach(() => {
    if (root) {
      act(() => {
        root?.unmount();
      });
      root = null;
    }
    container.remove();
  });

  const mockSettings: ServerSettingsDto = {
    id: 'settings-123',
    networkMode: NetworkMode.Localhost,
    listeningPortHttps: 5432,
    listeningPortHttp: 5431,
    selectedInterfaces: ['192.168.1.50'],
    theme: 'dark',
  };

  const mockNics: NetworkInterfaceDto[] = [
    { name: 'Ethernet', ipAddress: '192.168.1.50', status: 'Up' },
    { name: 'Loopback', ipAddress: '127.0.0.1', status: 'Up' },
  ];

  describe('Static Rendering', () => {
    it('renders loading state when initial settings are null', () => {
      const html = renderToString(
        <ToastProvider>
          <AuthProvider>
            <SettingsView />
          </AuthProvider>
        </ToastProvider>
      );

      expect(html).toContain('Loading settings...');
    });

    it('renders network configuration tab with interfaces', () => {
      const html = renderToString(
        <ToastProvider>
          <AuthProvider>
            <SettingsView initialSettings={mockSettings} initialNics={mockNics} initialSubTab="network" />
          </AuthProvider>
        </ToastProvider>
      );

      expect(html).toContain('Server &amp; Security Settings');
      expect(html).toContain('Network Configuration');
      expect(html).toContain('netModeSelect');
      expect(html).toContain('Localhost Only');
      expect(html).toContain('Available Server Network Interfaces');
      expect(html).toContain('Ethernet');
      expect(html).toContain('192.168.1.50');
      expect(html).toContain('saveSettingsBtn');
    });

    it('renders account management tab with change username and password forms', () => {
      const html = renderToString(
        <ToastProvider>
          <AuthProvider>
            <SettingsView initialSettings={mockSettings} initialNics={mockNics} initialSubTab="account" />
          </AuthProvider>
        </ToastProvider>
      );

      expect(html).toContain('Single-User Account Management');
      expect(html).toContain('Change Username');
      expect(html).toContain('accCurrentPassUser');
      expect(html).toContain('accNewUsername');
      expect(html).toContain('Change Password');
      expect(html).toContain('accCurrentPass');
      expect(html).toContain('accNewPass');
      expect(html).toContain('accConfirmPass');
    });

    it('renders logs and diagnostics tab', () => {
      const html = renderToString(
        <ToastProvider>
          <AuthProvider>
            <SettingsView initialSettings={mockSettings} initialNics={mockNics} initialSubTab="logs" />
          </AuthProvider>
        </ToastProvider>
      );

      expect(html).toContain('Logs &amp; Diagnostics');
    });
  });

  describe('Interactive Submissions & Handlers', () => {
    it('fetches settings on mount when no initial settings are provided', async () => {
      const apiFetchSpy = vi.spyOn(apiClient, 'apiFetch').mockImplementation(async (url: string) => {
        if (url === '/api/v1/settings') return { ok: true, status: 200, data: mockSettings } as any;
        if (url === '/api/v1/settings/network-interfaces') return { ok: true, status: 200, data: mockNics } as any;
        return { ok: true, status: 200, data: { isSetupComplete: true } } as any;
      });

      root = createRoot(container);
      await act(async () => {
        root?.render(
          <ToastProvider>
            <AuthProvider>
              <SettingsView />
            </AuthProvider>
          </ToastProvider>
        );
      });

      expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/settings');
      expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/settings/network-interfaces');
      expect(container.textContent).toContain('Server & Security Settings');
    });

    it('saves network settings successfully and handles API error', async () => {
      const apiFetchSpy = vi.spyOn(apiClient, 'apiFetch').mockImplementation(async (_url: string, opts?: any) => {
        if (opts?.method === 'PUT') {
          return { ok: true, status: 200, data: { success: true } } as any;
        }
        return { ok: true, status: 200, data: { isSetupComplete: true } } as any;
      });

      root = createRoot(container);
      await act(async () => {
        root?.render(
          <ToastProvider>
            <AuthProvider>
              <SettingsView initialSettings={mockSettings} initialNics={mockNics} initialSubTab="network" />
            </AuthProvider>
          </ToastProvider>
        );
      });

      // Change network mode
      const netModeSelect = container.querySelector('#netModeSelect') as HTMLSelectElement;
      await act(async () => {
        netModeSelect.value = 'Lan';
        netModeSelect.dispatchEvent(new Event('change', { bubbles: true }));
      });

      // Click Save Settings button
      const saveBtn = container.querySelector('#saveSettingsBtn') as HTMLButtonElement;
      await act(async () => {
        saveBtn.click();
      });

      expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/settings/settings-123', {
        method: 'PUT',
        body: expect.objectContaining({
          networkMode: 'Lan',
          listeningPortHttps: 5432,
          listeningPortHttp: 5431,
        }),
      });

      // Test save error
      apiFetchSpy.mockResolvedValueOnce({ ok: false, status: 500, error: 'Failed' } as any);
      await act(async () => {
        saveBtn.click();
      });
    });

    it('switches tabs between network, account, and logs', async () => {
      vi.spyOn(apiClient, 'apiFetch').mockImplementation(async (url: string) => {
        if (url.includes('/api/v1/logs')) return { ok: true, status: 200, data: [] } as any;
        return { ok: true, status: 200, data: { isSetupComplete: true } } as any;
      });

      root = createRoot(container);
      await act(async () => {
        root?.render(
          <ToastProvider>
            <AuthProvider>
              <SettingsView initialSettings={mockSettings} initialNics={mockNics} initialSubTab="network" />
            </AuthProvider>
          </ToastProvider>
        );
      });

      // Switch to Account tab
      const accountTabBtn = container.querySelector('#accountSettingsTabBtn') as HTMLButtonElement;
      expect(accountTabBtn).not.toBeNull();
      await act(async () => {
        accountTabBtn.click();
      });
      expect(container.textContent).toContain('Single-User Account Management');

      // Switch to Logs tab
      const logsTabBtn = container.querySelector('#logsSettingsTabBtn') as HTMLButtonElement;
      expect(logsTabBtn).not.toBeNull();
      await act(async () => {
        logsTabBtn.click();
      });
      expect(container.textContent).toContain('Logs & Diagnostics');
    });

    it('validates and submits Change Username form', async () => {
      const apiFetchSpy = vi.spyOn(apiClient, 'apiFetch').mockImplementation(async (_url: string, opts?: any) => {
        if (opts?.method === 'PUT') {
          return { ok: true, status: 200, data: { success: true, username: 'newadmin' } } as any;
        }
        return { ok: true, status: 200, data: { isSetupComplete: true } } as any;
      });

      root = createRoot(container);
      await act(async () => {
        root?.render(
          <ToastProvider>
            <AuthProvider>
              <SettingsView initialSettings={mockSettings} initialNics={mockNics} initialSubTab="account" />
            </AuthProvider>
          </ToastProvider>
        );
      });

      const currentPassUser = container.querySelector('#accCurrentPassUser') as HTMLInputElement;
      const newUsername = container.querySelector('#accNewUsername') as HTMLInputElement;
      const changeUserBtn = container.querySelector('#updateUsernameBtn') as HTMLButtonElement;
      expect(changeUserBtn).not.toBeNull();
      expect(changeUserBtn.disabled).toBe(true);

      // Fill current password
      await act(async () => {
        const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value')?.set;
        nativeSetter?.call(currentPassUser, 'secretPass123');
        currentPassUser.dispatchEvent(new Event('input', { bubbles: true }));
        currentPassUser.dispatchEvent(new Event('change', { bubbles: true }));
      });
      expect(changeUserBtn.disabled).toBe(true);

      // Fill new username
      await act(async () => {
        const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value')?.set;
        nativeSetter?.call(newUsername, 'newadmin');
        newUsername.dispatchEvent(new Event('input', { bubbles: true }));
        newUsername.dispatchEvent(new Event('change', { bubbles: true }));
      });
      expect(changeUserBtn.disabled).toBe(false);

      // Submit
      await act(async () => {
        changeUserBtn.click();
      });

      expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/auth/account/username', {
        method: 'PUT',
        body: {
          currentPassword: 'secretPass123',
          newUsername: 'newadmin',
        },
      });

      // Test error response
      apiFetchSpy.mockImplementation(async (_url: string, opts?: any) => {
        if (opts?.method === 'PUT') {
          return {
            ok: false,
            status: 400,
            error: 'Invalid password',
          } as any;
        }
        return { ok: true, status: 200, data: { isSetupComplete: true } } as any;
      });

      await act(async () => {
        const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value')?.set;
        nativeSetter?.call(currentPassUser, 'wrongPass');
        nativeSetter?.call(newUsername, 'otheradmin');
        currentPassUser.dispatchEvent(new Event('change', { bubbles: true }));
        newUsername.dispatchEvent(new Event('change', { bubbles: true }));
        changeUserBtn.click();
      });
    });

    it('validates and submits Change Password form', async () => {
      const apiFetchSpy = vi.spyOn(apiClient, 'apiFetch').mockImplementation(async (_url: string, opts?: any) => {
        if (opts?.method === 'PUT') {
          return { ok: true, status: 200, data: { success: true } } as any;
        }
        return { ok: true, status: 200, data: { isSetupComplete: true } } as any;
      });

      root = createRoot(container);
      await act(async () => {
        root?.render(
          <ToastProvider>
            <AuthProvider>
              <SettingsView initialSettings={mockSettings} initialNics={mockNics} initialSubTab="account" />
            </AuthProvider>
          </ToastProvider>
        );
      });

      const currentPass = container.querySelector('#accCurrentPass') as HTMLInputElement;
      const newPass = container.querySelector('#accNewPass') as HTMLInputElement;
      const confirmPass = container.querySelector('#accConfirmPass') as HTMLInputElement;
      const changePassBtn = container.querySelector('#updatePasswordBtn') as HTMLButtonElement;
      expect(changePassBtn).not.toBeNull();
      expect(changePassBtn.disabled).toBe(true);

      // Fill matching valid passwords
      await act(async () => {
        const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value')?.set;
        nativeSetter?.call(currentPass, 'oldPass123');
        currentPass.dispatchEvent(new Event('change', { bubbles: true }));
      });
      await act(async () => {
        const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value')?.set;
        nativeSetter?.call(newPass, 'newStrongPassword123');
        newPass.dispatchEvent(new Event('change', { bubbles: true }));
      });
      await act(async () => {
        const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value')?.set;
        nativeSetter?.call(confirmPass, 'newStrongPassword123');
        confirmPass.dispatchEvent(new Event('change', { bubbles: true }));
      });

      expect(changePassBtn.disabled).toBe(false);

      await act(async () => {
        changePassBtn.click();
      });

      expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/auth/account/password', {
        method: 'PUT',
        body: {
          currentPassword: 'oldPass123',
          newPassword: 'newStrongPassword123',
          confirmNewPassword: 'newStrongPassword123',
        },
      });

      // Test error response
      apiFetchSpy.mockImplementation(async (_url: string, opts?: any) => {
        if (opts?.method === 'PUT') {
          return {
            ok: false,
            status: 400,
            error: 'Current password invalid',
          } as any;
        }
        return { ok: true, status: 200, data: { isSetupComplete: true } } as any;
      });

      await act(async () => {
        const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value')?.set;
        nativeSetter?.call(currentPass, 'wrongPass');
        nativeSetter?.call(newPass, 'anotherValidPass123');
        nativeSetter?.call(confirmPass, 'anotherValidPass123');
        currentPass.dispatchEvent(new Event('change', { bubbles: true }));
        newPass.dispatchEvent(new Event('change', { bubbles: true }));
        confirmPass.dispatchEvent(new Event('change', { bubbles: true }));
        changePassBtn.click();
      });
    });
  });
});
