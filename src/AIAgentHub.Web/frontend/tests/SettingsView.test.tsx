import { describe, it, expect } from 'vitest';
import { renderToString } from 'react-dom/server';
import { SettingsView } from '../src/components/settings/SettingsView';
import { ServerSettingsDto, NetworkInterfaceDto, NetworkMode } from '../src/types/settings';
import { ToastProvider } from '../src/context/ToastContext';
import { AuthProvider } from '../src/context/AuthContext';

describe('SettingsView UI Component', () => {
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
