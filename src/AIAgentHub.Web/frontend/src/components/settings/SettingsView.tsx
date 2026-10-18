import React, { useState, useEffect } from 'react';
import { apiFetch } from '../../services/apiClient';
import { ServerSettingsDto, NetworkInterfaceDto, UpdateServerSettingsRequest, normalizeNetworkMode, NetworkModeType } from '../../types/settings';
import { useToast } from '../../context/ToastContext';
import { useAuth } from '../../context/AuthContext';
import { LogsViewer } from './LogsViewer';

export const SettingsView: React.FC = () => {
  const { showToast } = useToast();
  const { username, setUsername } = useAuth();
  const [activeSubTab, setActiveSubTab] = useState<'network' | 'logs' | 'account'>('network');
  const [settings, setSettings] = useState<ServerSettingsDto | null>(null);
  const [nics, setNics] = useState<NetworkInterfaceDto[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [isSaving, setIsSaving] = useState<boolean>(false);

  // Account management state
  const [currentPasswordForUsername, setCurrentPasswordForUsername] = useState<string>('');
  const [newUsernameInput, setNewUsernameInput] = useState<string>('');
  const [isUpdatingUsername, setIsUpdatingUsername] = useState<boolean>(false);

  const [currentPasswordForPass, setCurrentPasswordForPass] = useState<string>('');
  const [newPasswordInput, setNewPasswordInput] = useState<string>('');
  const [confirmPasswordInput, setConfirmPasswordInput] = useState<string>('');
  const [isUpdatingPassword, setIsUpdatingPassword] = useState<boolean>(false);

  useEffect(() => {
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
  }, []);

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
    if (!currentPasswordForUsername) {
      showToast('Current password is required to change username.', 'warning');
      return;
    }
    if (!newUsernameInput.trim()) {
      showToast('Please enter a new username.', 'warning');
      return;
    }
    setIsUpdatingUsername(true);
    try {
      const res = await apiFetch<{ success: boolean; username: string }>('/api/v1/auth/account/username', {
        method: 'PUT',
        body: {
          currentPassword: currentPasswordForUsername,
          newUsername: newUsernameInput.trim(),
        },
      });

      if (res.ok && res.data?.success) {
        showToast(`Username updated successfully to '${res.data.username}'.`, 'success');
        setUsername(res.data.username);
        setCurrentPasswordForUsername('');
        setNewUsernameInput('');
      } else {
        showToast((res.data as any)?.message || res.error || 'Failed to update username. Verify current password.', 'error');
      }
    } finally {
      setIsUpdatingUsername(false);
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
            <label className="form-label">Network Mode</label>
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
            <label className="form-label">Available Server Network Interfaces</label>
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
              Updating your username will refresh your authenticated session immediately.
            </p>
            <div className="form-group" style={{ marginBottom: '12px' }}>
              <label className="form-label" htmlFor="accCurrentPassUser">Current Password</label>
              <input
                type="password"
                id="accCurrentPassUser"
                name="current-password"
                autoComplete="current-password"
                className="form-control"
                placeholder="Enter current password"
                value={currentPasswordForUsername}
                onChange={(e) => setCurrentPasswordForUsername(e.target.value)}
                required
              />
            </div>
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
              disabled={isUpdatingUsername || !currentPasswordForUsername || !newUsernameInput}
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

      {activeSubTab === 'logs' && <LogsViewer />}
    </div>
  );
};
