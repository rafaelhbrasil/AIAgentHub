import React, { useState, useEffect } from 'react';
import { ConversationDetailDto, ConversationProviderSessionDto, SwitchProviderRequest, SwitchProviderResult, isUserRole } from '../../types/conversation';
import { ProviderDto, ModelInfo } from '../../types/provider';
import { apiFetch } from '../../services/apiClient';
import { useToast } from '../../context/ToastContext';
import { isProviderOperational } from '../../utils/providerSort';

interface SwitchProviderModalProps {
  conversation: ConversationDetailDto;
  onSuccess: (result: SwitchProviderResult) => void;
  onCancel: () => void;
}

interface SwitchConfigDto {
  recentMessageCounts: number[];
}

interface HistoryOption {
  id: string;
  label: string;
  count: number;
  disabled: boolean;
}

export const SwitchProviderModal: React.FC<SwitchProviderModalProps> = ({
  conversation,
  onSuccess,
  onCancel,
}) => {
  const { showToast } = useToast();
  const [providers, setProviders] = useState<ProviderDto[]>([]);
  const [targetProviderId, setTargetProviderId] = useState<string>('');
  const [models, setModels] = useState<ModelInfo[]>([]);
  const [selectedModelId, setSelectedModelId] = useState<string>('default');
  const [sessions, setSessions] = useState<ConversationProviderSessionDto[]>([]);
  const [recentCounts, setRecentCounts] = useState<number[]>([10, 20, 50]);
  const [historyScope, setHistoryScope] = useState<string>('delta');
  const [includeFileChanges, setIncludeFileChanges] = useState<boolean>(true);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);

  useEffect(() => {
    const loadData = async () => {
      setIsLoading(true);
      try {
        const [provRes, sessRes, configRes] = await Promise.all([
          apiFetch<ProviderDto[]>('/api/v1/providers'),
          apiFetch<ConversationProviderSessionDto[]>(`/api/v1/conversations/${conversation.id}/sessions`),
          apiFetch<SwitchConfigDto>('/api/v1/conversations/switch-config'),
        ]);

        if (provRes.ok && provRes.data) {
          const available = (provRes.data || []).filter(
            (p) => isProviderOperational(p) && p.id.toLowerCase() !== conversation.providerId.toLowerCase()
          );
          setProviders(available);
          if (available.length > 0) {
            setTargetProviderId(available[0].id);
          }
        }

        if (sessRes.ok && sessRes.data) {
          setSessions(sessRes.data);
        }

        if (configRes.ok && configRes.data?.recentMessageCounts) {
          const sorted = [...configRes.data.recentMessageCounts].filter((c) => c > 0).sort((a, b) => a - b);
          if (sorted.length > 0) {
            setRecentCounts(sorted);
          }
        }
      } catch (err: any) {
        showToast('Failed to load available providers.', 'error');
      } finally {
        setIsLoading(false);
      }
    };

    loadData();
  }, [conversation.id, conversation.providerId, showToast]);

  // Load models for target provider and set default history scope
  useEffect(() => {
    if (!targetProviderId) return;

    setHistoryScope('delta');

    const loadModels = async () => {
      const res = await apiFetch<ModelInfo[]>(`/api/v1/providers/${targetProviderId}/models`);
      if (res.ok && res.data) {
        setModels(res.data.filter((m) => m.isDisplayed !== false));
        setSelectedModelId('default');
      } else {
        setModels([]);
        setSelectedModelId('default');
      }
    };

    loadModels();
  }, [targetProviderId]);

  const targetSession = sessions.find(
    (s) => s.providerId.toLowerCase() === targetProviderId.toLowerCase()
  );
  const userPrompts = (conversation.messages || []).filter(
    (m) => isUserRole(m.role)
  );
  const totalInteractions = userPrompts.length;

  const diffInteractions = targetSession
    ? userPrompts.filter(
        (m) => (m.sequenceIndex || 0) > targetSession.lastSharedSequenceIndex
      ).length
    : totalInteractions;

  // Build ordered history scope options based on interaction turns (1 prompt + 1 response = 1 interaction)
  const historyOptions: HistoryOption[] = [
    {
      id: 'delta',
      label: targetSession
        ? `Differential (${diffInteractions} interaction${diffInteractions === 1 ? '' : 's'}) — Only unshared prompts & responses`
        : `Differential (${diffInteractions} interaction${diffInteractions === 1 ? '' : 's'}) — Full initial context`,
      count: diffInteractions,
      disabled: false,
    },
    ...recentCounts.map((count) => {
      let disabled = false;
      let note = '';
      if (targetSession) {
        if (diffInteractions <= count) {
          disabled = true;
          note = '(previously migrated)';
        }
      } else if (totalInteractions < count) {
        disabled = true;
        note = `(conversation only has ${totalInteractions} interaction${totalInteractions === 1 ? '' : 's'})`;
      }

      return {
        id: `recent_${count}`,
        label: `Recent ${count} interactions (${count} turns)${note ? ` — ${note}` : ` — Last ${count} prompts & responses`}`,
        count: Math.min(count, totalInteractions),
        disabled,
      };
    }),
    {
      id: 'all',
      label:
        targetSession && diffInteractions < totalInteractions
          ? `Full (${totalInteractions} interactions) — (previously migrated)`
          : `Full (${totalInteractions} interaction${totalInteractions === 1 ? '' : 's'}) — Complete conversation history`,
      count: totalInteractions,
      disabled: Boolean(targetSession && diffInteractions < totalInteractions),
    },
    {
      id: 'none',
      label: 'None (0 interactions) — Fresh session (history preserved for future migration)',
      count: 0,
      disabled: false,
    },
  ];

  // Auto-fallback if currently selected option becomes disabled
  useEffect(() => {
    const selected = historyOptions.find((o) => o.id === historyScope);
    if (selected && selected.disabled) {
      setHistoryScope('delta');
    }
  }, [historyScope, historyOptions]);

  const calculatePreviewCount = (): number => {
    const selected = historyOptions.find((o) => o.id === historyScope);
    return selected ? selected.count : diffInteractions;
  };

  const handleSwitch = async () => {
    if (!targetProviderId) return;
    setIsSubmitting(true);

    const request: SwitchProviderRequest = {
      targetProviderId,
      targetModelId: selectedModelId === 'default' ? null : selectedModelId,
      historyScope,
      includeFileChanges,
    };

    try {
      const res = await apiFetch<SwitchProviderResult>(
        `/api/v1/conversations/${conversation.id}/switch-provider`,
        {
          method: 'POST',
          body: request,
        }
      );

      if (res.ok && res.data) {
        showToast(
          `Switched to ${targetProviderId}. Replayed ${res.data.migratedMessageCount} interaction${res.data.migratedMessageCount === 1 ? '' : 's'}.`,
          'success'
        );
        onSuccess(res.data);
      } else {
        showToast(res.error || (res.data as any)?.message || 'Failed to switch provider.', 'error');
      }
    } catch (err: any) {
      showToast(err.message || 'Error executing provider switch.', 'error');
    } finally {
      setIsSubmitting(false);
    }
  };

  if (isLoading) {
    return <div style={{ padding: '20px', color: 'var(--text-muted)' }}>Loading available providers...</div>;
  }

  if (providers.length === 0) {
    return (
      <div style={{ padding: '16px', display: 'flex', flexDirection: 'column', gap: '14px' }}>
        <div
          style={{
            padding: '14px 16px',
            background: 'rgba(239, 68, 68, 0.1)',
            border: '1px solid rgba(239, 68, 68, 0.3)',
            borderRadius: '8px',
            color: '#f87171',
            fontSize: '0.9rem',
            lineHeight: 1.5,
          }}
        >
          ⚠️ <strong>No alternative AI providers ready to use.</strong>
          <div style={{ marginTop: '6px', color: 'var(--text-muted)', fontSize: '0.85rem' }}>
            You currently only have <strong>{conversation.providerId}</strong> ready to use. To switch between engines in a conversation, install and authenticate additional providers in the AI Providers settings.
          </div>
        </div>
        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
          <button type="button" className="btn btn-secondary" onClick={onCancel}>
            Close
          </button>
          <button
            type="button"
            className="btn btn-primary"
            onClick={() => {
              onCancel();
              window.history.pushState({}, '', '/providers');
              window.dispatchEvent(new PopStateEvent('popstate'));
            }}
          >
            Manage AI Providers
          </button>
        </div>
      </div>
    );
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
      <div style={{ fontSize: '0.88rem', color: 'var(--text-muted)' }}>
        Seamlessly transition this conversation from <strong>{conversation.providerId}</strong> to another AI CLI tool with preserved context and workspace changes.
      </div>

      <div className="form-group">
        <label className="form-label" htmlFor="targetProviderSelect">
          Target Provider
        </label>
        <select
          id="targetProviderSelect"
          className="form-control"
          value={targetProviderId}
          onChange={(e) => setTargetProviderId(e.target.value)}
          disabled={isSubmitting}
        >
          {providers.map((p) => (
            <option key={p.id} value={p.id}>
              {p.displayName} ({p.id})
            </option>
          ))}
        </select>
      </div>

      <div className="form-group">
        <label className="form-label" htmlFor="targetModelSelect">
          Target Model
        </label>
        <select
          id="targetModelSelect"
          className="form-control"
          value={selectedModelId}
          onChange={(e) => setSelectedModelId(e.target.value)}
          disabled={isSubmitting}
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
        <label className="form-label" htmlFor="historyScopeSelect">
          Conversation History Scope
        </label>
        <select
          id="historyScopeSelect"
          className="form-control"
          value={historyScope}
          onChange={(e) => setHistoryScope(e.target.value)}
          disabled={isSubmitting}
        >
          {historyOptions.map((opt) => (
            <option
              key={opt.id}
              value={opt.id}
              disabled={opt.disabled}
              className={opt.disabled ? 'scope-option-disabled' : 'scope-option-enabled'}
              style={
                opt.disabled
                  ? {
                      color: 'var(--text-muted, #64748b)',
                      backgroundColor: 'rgba(15, 23, 42, 0.85)',
                      fontStyle: 'italic',
                      opacity: 0.45,
                    }
                  : {
                      color: 'var(--text-main, #f8fafc)',
                      fontWeight: opt.id === 'delta' ? 600 : 400,
                    }
              }
            >
              {opt.label}
            </option>
          ))}
        </select>
      </div>

      <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
        <input
          type="checkbox"
          id="includeFileChangesCheckbox"
          checked={includeFileChanges}
          onChange={(e) => setIncludeFileChanges(e.target.checked)}
          disabled={isSubmitting}
          style={{ width: '16px', height: '16px', cursor: 'pointer' }}
        />
        <label htmlFor="includeFileChangesCheckbox" style={{ fontSize: '0.88rem', cursor: 'pointer' }}>
          Include summary of applied and pending workspace file changes in handoff context
        </label>
      </div>

      <div
        style={{
          background: 'var(--bg-glass)',
          border: '1px solid var(--border-color)',
          borderRadius: '8px',
          padding: '12px',
          fontSize: '0.85rem',
        }}
      >
        <div style={{ fontWeight: 600, marginBottom: '4px', color: 'var(--text-heading)' }}>
          Handoff Summary:
        </div>
        <ul style={{ margin: '0 0 0 18px', padding: 0, color: 'var(--text-muted)' }}>
          <li>
            Migrating <strong>{calculatePreviewCount()}</strong> / {totalInteractions} interactions (prompts & responses) to {targetProviderId}.
          </li>
          {historyScope === 'none' ? (
            <li style={{ color: 'var(--text-accent, #818cf8)' }}>
              ℹ️ Starting fresh session. Previous conversation history is preserved and will remain available to migrate if you switch back or don't execute prompts in {targetProviderId}.
            </li>
          ) : targetSession ? (
            <li>
              Existing session found (active on {new Date(targetSession.lastActiveAtUtc).toLocaleDateString()}).
            </li>
          ) : (
            <li>Starting a new provider session in workspace directory.</li>
          )}
          <li>
            Active model will be set to: <code>{selectedModelId}</code>.
          </li>
        </ul>
      </div>

      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px', marginTop: '8px' }}>
        <button type="button" className="btn btn-secondary" onClick={onCancel} disabled={isSubmitting}>
          Cancel
        </button>
        <button
          type="button"
          className="btn btn-primary"
          id="confirmSwitchProviderBtn"
          onClick={handleSwitch}
          disabled={isSubmitting}
        >
          {isSubmitting ? 'Switching Provider...' : 'Switch Provider'}
        </button>
      </div>
    </div>
  );
};

