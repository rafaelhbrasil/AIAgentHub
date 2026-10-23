import React, { useState, useEffect } from 'react';
import { apiFetch } from '../../services/apiClient';
import { ServerSettingsDto, NetworkInterfaceDto, UpdateServerSettingsRequest, normalizeNetworkMode, NetworkModeType } from '../../types/settings';
import { useToast } from '../../context/ToastContext';
import { useAuth } from '../../context/AuthContext';
import { LogsViewer } from './LogsViewer';

interface SettingsViewProps {
  initialSettings?: ServerSettingsDto;
  initialNics?: NetworkInterfaceDto[];
  initialSubTab?: 'network' | 'logs' | 'account';
}

export const SettingsView: React.FC<SettingsViewProps> = ({
  initialSettings,
  initialNics,
  initialSubTab = 'network',
}) => {
  const { showToast } = useToast();
  const { username, setUsername, setIsSetupCompleted, setIsAuthenticated, checkAuthAndSetup } = useAuth();
  const [activeSubTab, setActiveSubTab] = useState<'network' | 'logs' | 'account'>(initialSubTab);
  const [settings, setSettings] = useState<ServerSettingsDto | null>(initialSettings ?? null);
  const [nics, setNics] = useState<NetworkInterfaceDto[]>(initialNics ?? []);
  const [isLoading, setIsLoading] = useState<boolean>(!initialSettings);
  const [isSaving, setIsSaving] = useState<boolean>(false);

  // Account management state
  const [newUsernameInput, setNewUsernameInput] = useState<string>(username);
  const [isUpdatingUsername, setIsUpdatingUsername] = useState<boolean>(false);

  const [currentPasswordForPass, setCurrentPasswordForPass] = useState<string>('');
  const [newPasswordInput, setNewPasswordInput] = useState<string>('');
  const [confirmPasswordInput, setConfirmPasswordInput] = useState<string>('');
  const [isUpdatingPassword, setIsUpdatingPassword] = useState<boolean>(false);

  // Danger Zone full reset state
  const [isResetConfirmOpen, setIsResetConfirmOpen] = useState<boolean>(false);
  const [resetPassword, setResetPassword] = useState<string>('');
  const [isResetting, setIsResetting] = useState<boolean>(false);

  // Keep the username input pre-filled with the current username.
  useEffect(() => {
    setNewUsernameInput(username);
  }, [username]);

  useEffect(() => {
    if (initialSettings) return;
    const fetchSettings = async () => {
      setIsLoading(true);
      try {
        const [setRes, nicsRes] = await Promise.all([
          apiFetch<ServerSettingsDto>('/api/v1/settings'),
          apiFetch<NetworkInterfaceDto[]>('/api/v1/settings/network-interfaces'),
        ]);

        if (setRes.ok && setRes.data) setSettings(setRes.data);
        if (nicsRes.ok && nicsRes.data) setNics(nicsRes.data);
      } finally {
        setIsLoading(false);
      }
    };
    fetchSettings();
  }, [initialSettings]);

  const handleSave = async () => {
    if (!settings || !settings.id) return;
    setIsSaving(true);
    try {
      const mode = normalizeNetworkMode(settings.networkMode);
      const updatePayload: UpdateServerSettingsRequest = {
        networkMode: mode,
        listeningPortHttps: settings.listeningPortHttps,
        listeningPortHttp: settings.listeningPortHttp,
        selectedInterfaces: settings.selectedInterfaces,
        theme: settings.theme,
      };

      const res = await apiFetch(`/api/v1/settings/${settings.id}`, {
        method: 'PUT',
        body: updatePayload,
      });

      if (res.ok) {
        showToast('Settings saved successfully.', 'success');
      } else {
        showToast('Failed to save settings.', 'error');
      }
    } finally {
      setIsSaving(false);
    }
  };

  const handleUpdateUsername = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newUsernameInput.trim()) {
      showToast('Please enter a new username.', 'warning');
      return;
    }
    if (newUsernameInput.trim() === username) {
      showToast('Username is unchanged.', 'warning');
      return;
    }
    setIsUpdatingUsername(true);
    try {
      const res = await apiFetch<{ success: boolean; username: string }>('/api/v1/auth/account/username', {
        method: 'PUT',
        body: {
          newUsername: newUsernameInput.trim(),
        },
      });

      if (res.ok && res.data?.success) {
        showToast(`Username updated successfully to '${res.data.username}'.`, 'success');
        setUsername(res.data.username);
      } else {
        showToast((res.data as any)?.message || res.error || 'Failed to update username.', 'error');
      }
    } finally {
      setIsUpdatingUsername(false);
    }
  };

  const handleFullReset = async () => {
    if (!resetPassword) {
      showToast('Password is required to reset the system.', 'warning');
      return;
    }
    setIsResetting(true);
    try {
      const res = await apiFetch<{ success: boolean; message?: string }>('/api/v1/auth/reset-with-password', {
        method: 'POST',
        body: { password: resetPassword },
      });

      if (res.ok && res.data?.success) {
        showToast('System reset. All application data has been erased.', 'success');
        setIsSetupCompleted(false);
        setIsAuthenticated(false);
        await checkAuthAndSetup();
      } else {
        showToast((res.data as any)?.message || res.error || 'Failed to reset the system.', 'error');
      }
    } finally {
      setIsResetting(false);
    }
  };

  const handleUpdatePassword = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!currentPasswordForPass) {
      showToast('Current password is required.', 'warning');
      return;
    }
    if (newPasswordInput.length < 8) {
      showToast('New password must be at least 8 characters.', 'warning');
      return;
    }
    if (newPasswordInput !== confirmPasswordInput) {
      showToast('New passwords do not match.', 'warning');
      return;
    }
    setIsUpdatingPassword(true);
    try {
      const res = await apiFetch<{ success: boolean }>('/api/v1/auth/account/password', {
        method: 'PUT',
        body: {
          currentPassword: currentPasswordForPass,
          newPassword: newPasswordInput,
          confirmNewPassword: confirmPasswordInput,
        },
      });

      if (res.ok && res.data?.success) {
        showToast('Password updated successfully.', 'success');
        setCurrentPasswordForPass('');
        setNewPasswordInput('');
        setConfirmPasswordInput('');
      } else {
        showToast((res.data as any)?.message || res.error || 'Failed to update password. Verify current password.', 'error');
      }
    } finally {
      setIsUpdatingPassword(false);
    }
  };

  if (isLoading || !settings) {
    return <div style={{ color: 'var(--text-muted)', padding: '20px' }}>Loading settings...</div>;
  }

  const currentMode = normalizeNetworkMode(settings.networkMode);

  return (
    <div>
      <h2>Server & Security Settings</h2>
      <p className="card-subtitle">
        Manage HTTPS network interface listeners, TLS certificates, admin credentials and application logs.
      </p>

      {/* Settings Sub-tabs */}
      <div style={{ display: 'flex', gap: '8px', marginTop: '16px', marginBottom: '8px', flexWrap: 'wrap' }}>
        <button
          type="button"
          className={`btn compact-btn ${activeSubTab === 'network' ? 'btn-primary' : 'btn-secondary'}`}
          onClick={() => setActiveSubTab('network')}
          style={{ padding: '7px 16px', fontSize: '0.88rem' }}
          id="networkSettingsTabBtn"
        >
          🔒 Network & Security
        </button>
        <button
          type="button"
          className={`btn compact-btn ${activeSubTab === 'account' ? 'btn-primary' : 'btn-secondary'}`}
          onClick={() => setActiveSubTab('account')}
          style={{ padding: '7px 16px', fontSize: '0.88rem' }}
          id="accountSettingsTabBtn"
        >
          👤 Account Management
        </button>
        <button
          type="button"
          className={`btn compact-btn ${activeSubTab === 'logs' ? 'btn-primary' : 'btn-secondary'}`}
          onClick={() => setActiveSubTab('logs')}
          style={{ padding: '7px 16px', fontSize: '0.88rem' }}
          id="logsSettingsTabBtn"
        >
          📜 Logs & Diagnostics
        </button>
      </div>

      {activeSubTab === 'network' && (
        <div className="card glass" style={{ maxWidth: '800px', marginTop: '16px' }}>
          <div className="card-title">Network Configuration</div>
          <div className="form-group">
            <label className="form-label" htmlFor="netModeSelect">Network Mode</label>
            <select
              className="form-select"
              id="netModeSelect"
              value={currentMode}
              onChange={(e) => {
                const mode = e.target.value as NetworkModeType;
                setSettings((prev) => (prev ? { ...prev, networkMode: mode } : prev));
              }}
            >
              <option value="Localhost">Localhost Only (127.0.0.1)</option>
              <option value="Lan">LAN Access (All Interfaces)</option>
              <option value="SelectedInterfaces">Selected Interfaces Only</option>
            </select>
          </div>

          <div className="form-group">
            <div className="form-label">Available Server Network Interfaces</div>
            <div style={{ background: 'var(--bg-subtle)', border: '1px solid var(--border-color)', padding: '12px', borderRadius: '6px' }}>
              {currentMode === 'Localhost' && (
                <div style={{ color: 'var(--text-muted)', marginBottom: '8px', fontSize: '0.85rem' }}>
                  🔒 Server is in Localhost mode. Remote connections from other machines or WSL are rejected with 403 Forbidden.
                </div>
              )}
              {currentMode === 'Lan' && (
                <div style={{ color: 'var(--accent-success)', marginBottom: '8px', fontSize: '0.85rem' }}>
                  🌐 All active network interfaces below are automatically enabled for LAN access.
                </div>
              )}
              {currentMode === 'SelectedInterfaces' && (
                <div style={{ color: 'var(--accent-primary)', marginBottom: '8px', fontSize: '0.85rem' }}>
                  Select which network interfaces to allow connections from:
                </div>
              )}

              {nics.length > 0 ? (
                nics.map((n) => {
                  const isChecked = (settings.selectedInterfaces || []).includes(n.name) ||
                                    (settings.selectedInterfaces || []).includes(n.ipAddress);
                  return (
                    <div key={n.name + n.ipAddress} style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '8px' }}>
                      {currentMode === 'SelectedInterfaces' && (
                        <input
                          type="checkbox"
                          checked={isChecked}
                          onChange={(e) => {
                            const current = settings.selectedInterfaces || [];
                            const updated = e.target.checked
                              ? [...current.filter((x) => x !== n.name && x !== n.ipAddress), n.ipAddress]
                              : current.filter((x) => x !== n.name && x !== n.ipAddress);
                            setSettings((prev) => (prev ? { ...prev, selectedInterfaces: updated } : prev));
                          }}
                        />
                      )}
                      <div>
                        📶 <strong>{n.name}</strong> (<code>{n.ipAddress}</code>) — <span style={{ color: n.status === 'Up' ? 'var(--accent-success)' : 'var(--text-muted)' }}>{n.status}</span>
                      </div>
                    </div>
                  );
                })
              ) : (
                <div>127.0.0.1 (Localhost)</div>
              )}
            </div>
          </div>

          <div className="card-title" style={{ marginTop: '20px' }}>
            TLS Certificate & SANs
          </div>
          <p className="card-subtitle">
            Self-signed certificate generated with SANs covering localhost and LAN IP addresses.
          </p>

          <button
            type="button"
            className="btn btn-primary"
            id="saveSettingsBtn"
            onClick={handleSave}
            disabled={isSaving}
          >
            {isSaving ? 'Saving...' : 'Save Settings'}
          </button>
        </div>
      )}

      {activeSubTab === 'account' && (
        <div className="card glass" style={{ maxWidth: '800px', marginTop: '16px' }}>
          <div className="card-title">Single-User Account Management</div>
          <p className="card-subtitle">
            Update your admin credentials. Current username: <strong>{username}</strong>
          </p>

          {/* Change Username Form */}
          <form onSubmit={handleUpdateUsername} style={{ marginBottom: '28px', paddingBottom: '24px', borderBottom: '1px solid var(--border-color)' }}>
            <h3 style={{ fontSize: '1rem', marginBottom: '8px', color: 'var(--text-heading)' }}>Change Username</h3>
            <p style={{ fontSize: '0.82rem', color: 'var(--text-muted)', marginBottom: '12px' }}>
              The username field is pre-filled with your current username. Updating it refreshes your authenticated session immediately — no password needed.
            </p>
            <div className="form-group" style={{ marginBottom: '12px' }}>
              <label className="form-label" htmlFor="accNewUsername">New Username</label>
              <input
                type="text"
                id="accNewUsername"
                name="username"
                autoComplete="username"
                className="form-control"
                placeholder="Enter new username"
                value={newUsernameInput}
                onChange={(e) => setNewUsernameInput(e.target.value)}
                required
              />
            </div>
            <button
              type="submit"
              className="btn btn-primary compact-btn"
              id="updateUsernameBtn"
              disabled={isUpdatingUsername || !newUsernameInput.trim() || newUsernameInput.trim() === username}
            >
              {isUpdatingUsername ? 'Updating Username...' : 'Update Username'}
            </button>
          </form>

          {/* Change Password Form */}
          <form onSubmit={handleUpdatePassword} method="post" autoComplete="on">
            <h3 style={{ fontSize: '1rem', marginBottom: '8px', color: 'var(--text-heading)' }}>Change Password</h3>
            <p style={{ fontSize: '0.82rem', color: 'var(--text-muted)', marginBottom: '12px' }}>
              Passwords must be at least 8 characters long.
            </p>
            <div className="form-group" style={{ marginBottom: '12px' }}>
              <label className="form-label" htmlFor="accCurrentPass">Current Password</label>
              <input
                type="password"
                id="accCurrentPass"
                name="current-password"
                autoComplete="current-password"
                className="form-control"
                placeholder="Enter current password"
                value={currentPasswordForPass}
                onChange={(e) => setCurrentPasswordForPass(e.target.value)}
                required
              />
            </div>
            <div className="form-group" style={{ marginBottom: '12px' }}>
              <label className="form-label" htmlFor="accNewPass">New Password</label>
              <input
                type="password"
                id="accNewPass"
                name="new-password"
                autoComplete="new-password"
                className="form-control"
                placeholder="Enter new password (min. 8 characters)"
                value={newPasswordInput}
                onChange={(e) => setNewPasswordInput(e.target.value)}
                required
              />
            </div>
            <div className="form-group" style={{ marginBottom: '12px' }}>
              <label className="form-label" htmlFor="accConfirmPass">Confirm New Password</label>
              <input
                type="password"
                id="accConfirmPass"
                name="confirm-password"
                autoComplete="new-password"
                className="form-control"
                placeholder="Confirm new password"
                value={confirmPasswordInput}
                onChange={(e) => setConfirmPasswordInput(e.target.value)}
                required
              />
            </div>
            <button
              type="submit"
              className="btn btn-primary compact-btn"
              id="updatePasswordBtn"
              disabled={isUpdatingPassword || !currentPasswordForPass || !newPasswordInput || !confirmPasswordInput}
            >
              {isUpdatingPassword ? 'Updating Password...' : 'Update Password'}
            </button>
          </form>
        </div>
      )}

      {activeSubTab === 'account' && (
        <div
          className="card glass"
          style={{ maxWidth: '800px', marginTop: '16px', border: '1px solid rgba(239, 68, 68, 0.45)' }}
        >
          <div className="card-title" style={{ color: '#f87171' }}>Danger Zone</div>
          <p className="card-subtitle">
            Full system reset — irreversible. Requires your current password and an explicit confirmation.
          </p>

          {!isResetConfirmOpen ? (
            <button
              type="button"
              className="btn btn-danger"
              id="dangerZoneResetBtn"
              onClick={() => setIsResetConfirmOpen(true)}
            >
              Reset Entire Application&hellip;
            </button>
          ) : (
            <div
              style={{
                border: '1px solid rgba(239, 68, 68, 0.5)',
                borderRadius: '8px',
                padding: '16px',
                background: 'rgba(239, 68, 68, 0.06)',
              }}
            >
              <h4 style={{ color: '#f87171', margin: '0 0 8px 0', fontSize: '0.98rem' }}>
                Everything in the app will be lost
              </h4>
              <p style={{ fontSize: '0.85rem', color: 'var(--text-muted)', lineHeight: 1.55, margin: '0 0 10px 0' }}>
                Confirming permanently erases <strong>all application data</strong>: workspaces, conversations,
                messages, providers, and settings. You will be logged out and returned to the first-run setup.
                This operation cannot be undone.
              </p>
              <p style={{ fontSize: '0.85rem', color: '#fca5a5', margin: '0 0 6px 0', fontWeight: 600 }}>
                The following will NOT be deleted (data saved to disk):
              </p>
              <ul style={{ fontSize: '0.85rem', color: 'var(--text-muted)', lineHeight: 1.6, margin: '0 0 14px 0', paddingLeft: '20px' }}>
                <li>Files saved to disk inside workspace folders</li>
                <li>Git repositories</li>
                <li>Provider installations and configuration (Claude, Antigravity, and other CLIs are not uninstalled)</li>
              </ul>

              <div className="form-group" style={{ marginBottom: '12px' }}>
                <label className="form-label" htmlFor="dangerZonePassword">Current Password</label>
                <input
                  type="password"
                  id="dangerZonePassword"
                  name="current-password"
                  autoComplete="current-password"
                  className="form-control"
                  placeholder="Enter your current password"
                  value={resetPassword}
                  onChange={(e) => setResetPassword(e.target.value)}
                />
              </div>

              <div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                <button
                  type="button"
                  className="btn btn-secondary"
                  id="dangerZoneCancelBtn"
                  onClick={() => {
                    setIsResetConfirmOpen(false);
                    setResetPassword('');
                  }}
                  disabled={isResetting}
                >
                  Cancel
                </button>
                <button
                  type="button"
                  className="btn btn-danger"
                  id="dangerZoneConfirmBtn"
                  onClick={handleFullReset}
                  disabled={isResetting || !resetPassword}
                >
                  {isResetting ? 'Resetting...' : 'Confirm Full Reset'}
                </button>
              </div>
            </div>
          )}
        </div>
      )}

      {activeSubTab === 'logs' && <LogsViewer />}
    </div>
  );
};
