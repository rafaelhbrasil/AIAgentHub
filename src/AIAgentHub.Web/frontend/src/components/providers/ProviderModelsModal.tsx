import React, { useState, useEffect } from 'react';
import { apiFetch } from '../../services/apiClient';
import { ModelInfo, ProviderDto } from '../../types/provider';
import { useToast } from '../../context/ToastContext';

interface ProviderModelsModalProps {
  provider: ProviderDto;
  initialModels: ModelInfo[];
  onSaveSuccess: (updatedModels: ModelInfo[]) => void;
  onCancel: () => void;
}

export const ProviderModelsModal: React.FC<ProviderModelsModalProps> = ({
  provider,
  initialModels,
  onSaveSuccess,
  onCancel,
}) => {
  const { showToast } = useToast();
  const [models, setModels] = useState<ModelInfo[]>(initialModels || []);
  const [modelStates, setModelStates] = useState<Record<string, boolean>>({});
  const [searchQuery, setSearchQuery] = useState<string>('');
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [isSaving, setIsSaving] = useState<boolean>(false);

  const filterDefaultModels = (list: ModelInfo[]) =>
    list.filter((m) => m.id && m.id.toLowerCase() !== 'default');

  useEffect(() => {
    const fetchModelsIfNeeded = async () => {
      if (initialModels && initialModels.length > 0) {
        const cleanInitial = filterDefaultModels(initialModels);
        const stateMap: Record<string, boolean> = {};
        cleanInitial.forEach((m) => {
          stateMap[m.id] = m.isDisplayed !== false;
        });
        setModelStates(stateMap);
        setModels(cleanInitial);
        return;
      }

      setIsLoading(true);
      try {
        const res = await apiFetch<ModelInfo[]>(`/api/v1/providers/${provider.id}/models`);
        const raw = res.ok && res.data ? res.data : provider.supportedModels || [];
        const loadedModels = filterDefaultModels(raw);
        setModels(loadedModels);
        const stateMap: Record<string, boolean> = {};
        loadedModels.forEach((m) => {
          stateMap[m.id] = m.isDisplayed !== false;
        });
        setModelStates(stateMap);
      } finally {
        setIsLoading(false);
      }
    };
    fetchModelsIfNeeded();
  }, [provider, initialModels]);

  const handleToggle = (id: string, checked: boolean) => {
    setModelStates((prev) => ({ ...prev, [id]: checked }));
  };

  const handleSelectAll = (checked: boolean) => {
    const newStates: Record<string, boolean> = {};
    models.forEach((m) => {
      newStates[m.id] = checked;
    });
    setModelStates(newStates);
  };

  const handleSave = async () => {
    setIsSaving(true);
    try {
      const res = await apiFetch(`/api/v1/providers/${provider.id}/models/settings`, {
        method: 'PUT',
        body: { modelStates },
      });

      if (res.ok) {
        // Reload updated models from DB (no refresh query param)
        const freshRes = await apiFetch<ModelInfo[]>(`/api/v1/providers/${provider.id}/models`);
        const rawFresh = freshRes.ok && freshRes.data ? freshRes.data : models.map((m) => ({
          ...m,
          isDisplayed: modelStates[m.id] ?? true,
        }));
        const freshModels = filterDefaultModels(rawFresh);
        showToast('Model settings saved successfully.', 'success');
        onSaveSuccess(freshModels);
      } else {
        showToast('Failed to save model settings.', 'error');
      }
    } finally {
      setIsSaving(false);
    }
  };

  const filteredModels = models.filter((m) => {
    const query = searchQuery.toLowerCase().trim();
    if (!query) return true;
    const name = (m.displayName || m.id).toLowerCase();
    return name.includes(query) || m.id.toLowerCase().includes(query);
  });

  const activeCount = Object.values(modelStates).filter(Boolean).length;

  return (
    <div>
      <div
        style={{
          marginBottom: '12px',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          flexWrap: 'wrap',
          gap: '8px',
        }}
      >
        <div style={{ fontSize: '0.88rem', color: 'var(--text-muted)' }}>
          {isLoading ? (
            'Loading models...'
          ) : (
            <>
              <strong>{activeCount}</strong> of <strong>{models.length}</strong> models configured as
              active (displayed).
            </>
          )}
        </div>
        <div style={{ display: 'flex', gap: '8px' }}>
          <button
            type="button"
            className="btn btn-secondary"
            style={{ padding: '4px 10px', fontSize: '0.8rem' }}
            onClick={() => handleSelectAll(true)}
          >
            Select All (ON)
          </button>
          <button
            type="button"
            className="btn btn-secondary"
            style={{ padding: '4px 10px', fontSize: '0.8rem' }}
            onClick={() => handleSelectAll(false)}
          >
            Deselect All (OFF)
          </button>
        </div>
      </div>

      <div className="form-group" style={{ marginBottom: '12px' }}>
        <input
          type="text"
          className="form-input"
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
          placeholder="🔍 Search models by name or ID..."
        />
      </div>

      <div className="model-list-container">
        {filteredModels.length === 0 ? (
          <div style={{ color: 'var(--text-muted)', padding: '16px', textAlign: 'center' }}>
            No models found.
          </div>
        ) : (
          filteredModels.map((m) => (
            <div key={m.id} className="model-row">
              <div className="model-row-info">
                <div className="model-row-title">
                  {m.displayName || m.id}
                  {m.isDefault && (
                    <span className="badge badge-provider" style={{ marginLeft: '6px', fontSize: '0.7rem' }}>
                      Default
                    </span>
                  )}
                </div>
                <div className="model-row-id">
                  <code>{m.id}</code>
                </div>
              </div>
              <label className="toggle-switch" title="Toggle model visibility in assistant selectors">
                <input
                  type="checkbox"
                  checked={modelStates[m.id] !== false}
                  onChange={(e) => handleToggle(m.id, e.target.checked)}
                />
                <span className="toggle-slider"></span>
              </label>
            </div>
          ))
        )}
      </div>

      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px', marginTop: '16px' }}>
        <button type="button" className="btn btn-secondary" onClick={onCancel}>
          Cancel
        </button>
        <button
          type="button"
          className="btn btn-primary"
          onClick={handleSave}
          disabled={isSaving}
        >
          {isSaving ? 'Saving...' : 'Save Configuration'}
        </button>
      </div>
    </div>
  );
};
