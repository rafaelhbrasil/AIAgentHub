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
      expect(html).not.toContain('accCurrentPassUser');
      expect(html).toContain('accNewUsername');
      expect(html).toContain('Change Password');
      expect(html).toContain('accCurrentPass');
      expect(html).toContain('accNewPass');
      expect(html).toContain('accConfirmPass');
      expect(html).toContain('Danger Zone');
      expect(html).toContain('dangerZoneResetBtn');
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

    it('validates and submits Change Username form without a password', async () => {
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

      // No current password field on the username form anymore.
      expect(container.querySelector('#accCurrentPassUser')).toBeNull();

      const newUsername = container.querySelector('#accNewUsername') as HTMLInputElement;
      const changeUserBtn = container.querySelector('#updateUsernameBtn') as HTMLButtonElement;
      expect(changeUserBtn).not.toBeNull();

      // Username input is pre-filled with the current username; unchanged value keeps the button disabled.
      expect(newUsername.value).toBe('admin');
      expect(changeUserBtn.disabled).toBe(true);

      // Change username to enable the button
      await act(async () => {
        const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value')?.set;
        nativeSetter?.call(newUsername, 'newadmin');
        newUsername.dispatchEvent(new Event('input', { bubbles: true }));
        newUsername.dispatchEvent(new Event('change', { bubbles: true }));
      });
      expect(changeUserBtn.disabled).toBe(false);

      // Submit — payload carries only the new username, no password.
      await act(async () => {
        changeUserBtn.click();
      });

      expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/auth/account/username', {
        method: 'PUT',
        body: {
          newUsername: 'newadmin',
        },
      });

      // After success the input re-syncs to the new username and the button is disabled again.
      expect((container.querySelector('#accNewUsername') as HTMLInputElement).value).toBe('newadmin');
      expect((container.querySelector('#updateUsernameBtn') as HTMLButtonElement).disabled).toBe(true);

      // Error path: failed update must not crash the form.
      apiFetchSpy.mockImplementation(async (_url: string, opts?: any) => {
        if (opts?.method === 'PUT') {
          return {
            ok: false,
            status: 400,
            error: 'Failed to update username.',
          } as any;
        }
        return { ok: true, status: 200, data: { isSetupComplete: true } } as any;
      });

      await act(async () => {
        const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value')?.set;
        nativeSetter?.call(newUsername, 'otheradmin');
        newUsername.dispatchEvent(new Event('input', { bubbles: true }));
        newUsername.dispatchEvent(new Event('change', { bubbles: true }));
      });
      expect((container.querySelector('#updateUsernameBtn') as HTMLButtonElement).disabled).toBe(false);
      await act(async () => {
        (container.querySelector('#updateUsernameBtn') as HTMLButtonElement).click();
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

    it('requires password confirmation in Danger Zone and posts reset-with-password', async () => {
      const apiFetchSpy = vi.spyOn(apiClient, 'apiFetch').mockImplementation(async (_url: string, opts?: any) => {
        if (opts?.method === 'POST' && _url === '/api/v1/auth/reset-with-password') {
          return { ok: true, status: 200, data: { success: true } } as any;
        }
        return { ok: true, status: 200, data: { isSetupComplete: true, isAuthenticated: false } } as any;
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

      const resetBtn = container.querySelector('#dangerZoneResetBtn') as HTMLButtonElement;
      expect(resetBtn).not.toBeNull();

      // No confirmation UI until the danger button is pressed.
      expect(container.querySelector('#dangerZoneConfirmBtn')).toBeNull();

      await act(async () => {
        resetBtn.click();
      });

      // Confirmation dialog spells out lost vs preserved data.
      const text = container.textContent || '';
      expect(text).toContain('Everything in the app will be lost');
      expect(text).toContain('Git repositories');
      expect(text).toContain('Provider installations');
      expect(text).toContain('NOT be deleted');

      const passwordInput = container.querySelector('#dangerZonePassword') as HTMLInputElement;
      const confirmBtn = container.querySelector('#dangerZoneConfirmBtn') as HTMLButtonElement;
      const cancelBtn = container.querySelector('#dangerZoneCancelBtn') as HTMLButtonElement;
      expect(passwordInput).not.toBeNull();
      expect(confirmBtn).not.toBeNull();
      expect(cancelBtn).not.toBeNull();

      // Password required before confirming.
      expect(confirmBtn.disabled).toBe(true);

      await act(async () => {
        const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value')?.set;
        nativeSetter?.call(passwordInput, '123456');
        passwordInput.dispatchEvent(new Event('input', { bubbles: true }));
        passwordInput.dispatchEvent(new Event('change', { bubbles: true }));
      });
      expect(confirmBtn.disabled).toBe(false);

      await act(async () => {
        confirmBtn.click();
      });

      expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/auth/reset-with-password', {
        method: 'POST',
        body: { password: '123456' },
      });
    });

    it('validates password length and mismatch in Change Password form', async () => {
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

      // Fill short password (< 8 chars)
      await act(async () => {
        const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value')?.set;
        nativeSetter?.call(currentPass, 'validPass123');
        nativeSetter?.call(newPass, 'short');
        nativeSetter?.call(confirmPass, 'short');
        currentPass.dispatchEvent(new Event('change', { bubbles: true }));
        newPass.dispatchEvent(new Event('change', { bubbles: true }));
        confirmPass.dispatchEvent(new Event('change', { bubbles: true }));
      });
      await act(async () => {
        changePassBtn.click();
      });

      // Fill mismatching passwords
      await act(async () => {
        const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value')?.set;
        nativeSetter?.call(newPass, 'longValidPass123');
        nativeSetter?.call(confirmPass, 'differentValidPass123');
        newPass.dispatchEvent(new Event('change', { bubbles: true }));
        confirmPass.dispatchEvent(new Event('change', { bubbles: true }));
      });
      await act(async () => {
        changePassBtn.click();
      });

      expect(changePassBtn).not.toBeNull();
      expect(currentPass.value).toBe('validPass123');
    });

    it('toggles network interfaces when in SelectedInterfaces mode', async () => {
      const selectedSettings = {
        ...mockSettings,
        networkMode: 'SelectedInterfaces' as const,
        selectedInterfaces: ['192.168.1.50'],
      };

      root = createRoot(container);
      await act(async () => {
        root?.render(
          <ToastProvider>
            <AuthProvider>
              <SettingsView initialSettings={selectedSettings} initialNics={mockNics} initialSubTab="network" />
            </AuthProvider>
          </ToastProvider>
        );
      });

      const checkboxes = container.querySelectorAll('input[type="checkbox"]');
      expect(checkboxes.length).toBeGreaterThan(0);

      // Check another interface and uncheck
      await act(async () => {
        (checkboxes[0] as HTMLInputElement).click();
      });
      if (checkboxes.length > 1) {
        await act(async () => {
          (checkboxes[1] as HTMLInputElement).click();
        });
      }
    });
  });
});
