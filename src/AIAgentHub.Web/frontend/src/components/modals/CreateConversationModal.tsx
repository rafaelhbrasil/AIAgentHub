import React, { useState, useEffect } from 'react';
import { apiFetch } from '../../services/apiClient';
import { ModelInfo, ProviderDto } from '../../types/provider';
import { isProviderOperational } from '../../utils/providerSort';

interface CreateConversationModalProps {
  defaultProviderId: string;
  defaultModelId?: string;
  onSubmit: (title: string, providerId: string, modelId?: string) => Promise<void>;
  onCancel: () => void;
}

export const CreateConversationModal: React.FC<CreateConversationModalProps> = ({
  defaultProviderId,
  defaultModelId,
  onSubmit,
  onCancel,
}) => {
  const [title, setTitle] = useState<string>('');
  const [availableProviders, setAvailableProviders] = useState<ProviderDto[]>([]);
  const [providerId, setProviderId] = useState<string>(defaultProviderId || '');
  const [modelId, setModelId] = useState<string>(defaultModelId || '');
  const [models, setModels] = useState<ModelInfo[]>([]);
  const [isLoadingProviders, setIsLoadingProviders] = useState<boolean>(true);
  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);

  useEffect(() => {
    const fetchProviders = async () => {
      setIsLoadingProviders(true);
      try {
        const res = await apiFetch<ProviderDto[]>('/api/v1/providers');
        if (res.ok && res.data) {
          const operational = res.data.filter(isProviderOperational);
          setAvailableProviders(operational);
          if (operational.length > 0) {
            const hasDefault = operational.some((p) => p.id === defaultProviderId);
            setProviderId(hasDefault ? defaultProviderId : operational[0].id);
          } else {
            setProviderId('');
          }
        }
      } finally {
        setIsLoadingProviders(false);
      }
    };
    fetchProviders();
  }, [defaultProviderId]);

  useEffect(() => {
    const loadModels = async () => {
      if (!providerId) {
        setModels([]);
        return;
      }
      const res = await apiFetch<ModelInfo[]>(`/api/v1/providers/${providerId}/models`);
      if (res.ok && res.data) {
        setModels(res.data.filter((m) => m.isDisplayed !== false));
      } else {
        setModels([]);
      }
    };
    loadModels();
  }, [providerId]);

  const handleSubmit = async (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    const cleanTitle = title.trim();
    if (!cleanTitle || !providerId) return;

    setIsSubmitting(true);
    try {
      await onSubmit(cleanTitle, providerId, modelId || undefined);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <form onSubmit={handleSubmit}>
      <div className="form-group">
        <label className="form-label">Conversation Topic</label>
        <input
          type="text"
          className="form-input"
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          placeholder="e.g. Implement user authentication, Fix bug in API..."
          autoFocus
          required
        />
      </div>

      <div className="form-group">
        <label className="form-label">AI Assistant Provider</label>
        {isLoadingProviders ? (
          <div style={{ color: 'var(--text-muted)', fontSize: '0.88rem', padding: '6px 0' }}>
            Loading available providers...
          </div>
        ) : availableProviders.length === 0 ? (
          <div
            style={{
              padding: '10px 14px',
              background: 'rgba(239, 68, 68, 0.1)',
              border: '1px solid rgba(239, 68, 68, 0.3)',
              borderRadius: '6px',
              color: '#f87171',
              fontSize: '0.85rem',
              lineHeight: 1.5,
            }}
          >
            ⚠️ No operational AI providers are currently available. Please install and authenticate a provider in the{' '}
            <a
              href="/providers"
              onClick={(e) => {
                e.preventDefault();
                onCancel();
                window.history.pushState({}, '', '/providers');
                window.dispatchEvent(new PopStateEvent('popstate'));
              }}
              style={{ color: '#60a5fa', textDecoration: 'underline', fontWeight: 600, cursor: 'pointer' }}
            >
              AI Providers
            </a>{' '}
            page first.
          </div>
        ) : (
          <select
            className="form-select"
            id="convProviderSelect"
            value={providerId}
            onChange={(e) => {
              setProviderId(e.target.value);
              setModelId('');
            }}
          >
            {availableProviders.map((p) => (
              <option key={p.id} value={p.id}>
                {p.displayName}
              </option>
            ))}
          </select>
        )}
      </div>

      {models.length > 0 && (
        <div className="form-group">
          <label className="form-label">Model (Optional)</label>
          <select
            className="form-select"
            value={modelId}
            onChange={(e) => setModelId(e.target.value)}
          >
            <option value="">Default Model</option>
            {models
              .filter((m) => m.id && m.id.toLowerCase() !== 'default')
              .map((m) => (
                <option key={m.id} value={m.id}>
                  {m.displayName || m.id}
                </option>
              ))}
          </select>
        </div>
      )}

      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px', marginTop: '20px' }}>
        <button type="button" className="btn btn-secondary" onClick={onCancel} disabled={isSubmitting}>
          Cancel
        </button>
        <button
          type="submit"
          className="btn btn-primary"
          id="confirmCreateConvBtn"
          disabled={isSubmitting || !title.trim() || !providerId || availableProviders.length === 0}
        >
          {isSubmitting ? 'Creating...' : 'Create Conversation'}
        </button>
      </div>
    </form>
  );
};
