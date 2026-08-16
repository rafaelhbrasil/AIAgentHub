import React, { useState, useEffect } from 'react';
import { apiFetch } from '../../services/apiClient';
import { ModelInfo } from '../../types/provider';

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
  const [title, setTitle] = useState<string>('New Feature Task');
  const [providerId, setProviderId] = useState<string>(defaultProviderId || 'antigravity');
  const [modelId, setModelId] = useState<string>(defaultModelId || '');
  const [models, setModels] = useState<ModelInfo[]>([]);
  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);

  useEffect(() => {
    const loadModels = async () => {
      if (!providerId) return;
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
    if (!cleanTitle) return;

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
        <select
          className="form-select"
          value={providerId}
          onChange={(e) => {
            setProviderId(e.target.value);
            setModelId('');
          }}
        >
          <option value="antigravity">Antigravity CLI — Google DeepMind</option>
          <option value="gemini">Gemini CLI</option>
          <option value="codex">OpenAI Codex CLI</option>
          <option value="claude">Claude Code</option>
          <option value="opencode">OpenCode</option>
        </select>
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
            {models.map((m) => (
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
          disabled={isSubmitting || !title.trim()}
        >
          {isSubmitting ? 'Creating...' : 'Create Conversation'}
        </button>
      </div>
    </form>
  );
};
