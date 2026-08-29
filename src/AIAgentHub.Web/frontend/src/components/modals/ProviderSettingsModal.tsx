import React, { useState } from 'react';
import { ProviderDto, ModelInfo } from '../../types/provider';
import { apiFetch } from '../../services/apiClient';
import { useToast } from '../../context/ToastContext';

interface ProviderSettingsModalProps {
  provider: ProviderDto;
  onSaved: (updated: ProviderDto) => void;
  onCancel: () => void;
}

export const ProviderSettingsModal: React.FC<ProviderSettingsModalProps> = ({
  provider,
  onSaved,
  onCancel,
}) => {
  const { showToast } = useToast();
  const [isHidden, setIsHidden] = useState<boolean>(provider.isHidden ?? false);
  const [defaultModelId, setDefaultModelId] = useState<string>(provider.defaultModelId || 'default');
  const [defaultEffort, setDefaultEffort] = useState<string>(provider.defaultEffort || '');
  const [models, setModels] = useState<ModelInfo[]>(provider.supportedModels || []);
  const [isSaving, setIsSaving] = useState<boolean>(false);

  const handleToggleModelVisibility = (modelId: string, isDisplayed: boolean) => {
    setModels((prev) =>
      prev.map((m) => (m.id === modelId ? { ...m, isDisplayed } : m))
    );
  };

  const handleSave = async () => {
    setIsSaving(true);
    const modelVisibility: Record<string, boolean> = {};
    for (const m of models) {
      modelVisibility[m.id] = m.isDisplayed !== false;
    }

    try {
      const res = await apiFetch<ProviderDto>(`/api/v1/providers/${provider.id}/settings`, {
        method: 'PUT',
        body: {
          isHidden,
          defaultModelId: defaultModelId === 'default' ? null : defaultModelId,
          defaultEffort: defaultEffort || null,
          modelVisibility,
        },
      });

      if (res.ok && res.data) {
        showToast(`Saved settings for ${provider.displayName}.`, 'success');
        onSaved(res.data);
      } else {
        showToast(res.error || 'Failed to save provider settings.', 'error');
      }
    } catch (err: any) {
      showToast(err.message || 'Error saving provider settings.', 'error');
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
      <div style={{ fontSize: '0.88rem', color: 'var(--text-muted)' }}>
        Configure default parameters and visibility for <strong>{provider.displayName}</strong>.
      </div>

      <div style={{ display: 'flex', alignItems: 'center', gap: '10px', padding: '8px 0' }}>
        <input
          type="checkbox"
          id="hideProviderCheckbox"
          checked={isHidden}
          onChange={(e) => setIsHidden(e.target.checked)}
          style={{ width: '16px', height: '16px', cursor: 'pointer' }}
        />
        <label htmlFor="hideProviderCheckbox" style={{ fontSize: '0.88rem', cursor: 'pointer' }}>
          <strong>Hide Provider</strong> — Do not show in conversation creation or switch provider dropdowns
        </label>
      </div>

      <div className="form-group">
        <label className="form-label" htmlFor="defaultModelSelect">
          Default Model for New Conversations
        </label>
        <select
          id="defaultModelSelect"
          className="form-control"
          value={defaultModelId}
          onChange={(e) => setDefaultModelId(e.target.value)}
        >
          <option value="default">Default (Provider CLI default)</option>
          {models.map((m) => (
            <option key={m.id} value={m.id}>
              {m.displayName || m.id}
            </option>
          ))}
        </select>
      </div>

      <div className="form-group">
        <label className="form-label" htmlFor="defaultEffortSelect">
          Default Reasoning Effort
        </label>
        <select
          id="defaultEffortSelect"
          className="form-control"
          value={defaultEffort}
          onChange={(e) => setDefaultEffort(e.target.value)}
        >
          <option value="">Default (Provider CLI default)</option>
          <option value="low">Low Effort</option>
          <option value="medium">Medium Effort</option>
          <option value="high">High Effort</option>
          <option value="max">Max Effort</option>
        </select>
      </div>

      {models.length > 0 && (
        <div className="form-group">
          <label className="form-label" style={{ marginBottom: '8px' }}>
            Model Visibility ({models.filter((m) => m.isDisplayed !== false).length} visible)
          </label>
          <div
            style={{
              maxHeight: '160px',
              overflowY: 'auto',
              border: '1px solid var(--border-color)',
              borderRadius: '6px',
              padding: '8px',
              background: 'var(--bg-glass)',
            }}
          >
            {models.map((m) => (
              <div
                key={m.id}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: '8px',
                  padding: '4px 0',
                  fontSize: '0.85rem',
                }}
              >
                <input
                  type="checkbox"
                  id={`model-vis-${m.id}`}
                  checked={m.isDisplayed !== false}
                  onChange={(e) => handleToggleModelVisibility(m.id, e.target.checked)}
                  style={{ cursor: 'pointer' }}
                />
                <label htmlFor={`model-vis-${m.id}`} style={{ cursor: 'pointer', flex: 1 }}>
                  {m.displayName || m.id}
                </label>
              </div>
            ))}
          </div>
        </div>
      )}

      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px', marginTop: '8px' }}>
        <button type="button" className="btn btn-secondary" onClick={onCancel} disabled={isSaving}>
          Cancel
        </button>
        <button
          type="button"
          className="btn btn-primary"
          id="saveProviderSettingsBtn"
          onClick={handleSave}
          disabled={isSaving}
        >
          {isSaving ? 'Saving...' : 'Save Settings'}
        </button>
      </div>
    </div>
  );
};
