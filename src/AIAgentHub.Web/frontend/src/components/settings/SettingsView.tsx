import React, { useState, useEffect } from 'react';
import { apiFetch } from '../../services/apiClient';
import { ServerSettingsDto, NetworkInterfaceDto, UpdateServerSettingsRequest } from '../../types/settings';
import { useToast } from '../../context/ToastContext';

export const SettingsView: React.FC = () => {
  const { showToast } = useToast();
  const [settings, setSettings] = useState<ServerSettingsDto | null>(null);
  const [nics, setNics] = useState<NetworkInterfaceDto[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [isSaving, setIsSaving] = useState<boolean>(false);

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
      const updatePayload: UpdateServerSettingsRequest = {
        networkMode: settings.networkMode,
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

  if (isLoading || !settings) {
    return <div style={{ color: 'var(--text-muted)', padding: '20px' }}>Loading settings...</div>;
  }

  return (
    <div>
      <h2>Server & Security Settings</h2>
      <p className="card-subtitle">
        Manage HTTPS network interface listeners, TLS certificates and administrator recovery.
      </p>

      <div className="card glass" style={{ maxWidth: '800px', marginTop: '20px' }}>
        <div className="card-title">Network Configuration</div>
        <div className="form-group">
          <label className="form-label">Network Mode</label>
          <select
            className="form-select"
            id="netModeSelect"
            value={settings.networkMode}
            onChange={(e) =>
              setSettings((prev) => (prev ? { ...prev, networkMode: parseInt(e.target.value, 10) } : prev))
            }
          >
            <option value={0}>Localhost Only (127.0.0.1)</option>
            <option value={1}>LAN Access (All Interfaces)</option>
          </select>
        </div>

        <div className="form-group">
          <label className="form-label">Available Server Network Interfaces</label>
          <div style={{ background: 'rgba(0,0,0,0.3)', padding: '12px', borderRadius: '6px' }}>
            {nics.length > 0 ? (
              nics.map((n) => (
                <div key={n.name + n.ipAddress} style={{ marginBottom: '4px' }}>
                  📶 <strong>{n.name}</strong> ({n.ipAddress}) - {n.status}
                </div>
              ))
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
    </div>
  );
};
