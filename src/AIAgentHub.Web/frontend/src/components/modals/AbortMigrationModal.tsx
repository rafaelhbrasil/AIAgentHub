import React, { useState, useEffect } from 'react';
import { ConversationDetailDto, ConversationStatus } from '../../types/conversation';
import { apiFetch } from '../../services/apiClient';
import { useToast } from '../../context/ToastContext';

interface AbortMigrationModalProps {
  conversation: ConversationDetailDto;
  onSuccess: (updatedConv?: ConversationDetailDto) => void;
  onClose: () => void;
}

export const AbortMigrationModal: React.FC<AbortMigrationModalProps> = ({
  conversation,
  onSuccess,
  onClose,
}) => {
  const [isAborting, setIsAborting] = useState(false);
  const { showToast } = useToast();

  const isSwitching =
    conversation.status === ConversationStatus.SwitchingProvider ||
    (conversation.status as any) === 1;

  // Realtime listener: If migration finishes while modal is open, automatically close
  useEffect(() => {
    if (!isSwitching) {
      onClose();
      showToast('Provider migration completed.', 'info');
    }
  }, [isSwitching, onClose, showToast]);

  const handleAbort = async () => {
    setIsAborting(true);
    try {
      const res = await apiFetch<ConversationDetailDto>(
        `/api/v1/conversations/${conversation.id}/abort-switch`,
        { method: 'POST' }
      );

      if (res.ok) {
        showToast('Provider switch aborted. Reverted to previous provider.', 'success');
        onSuccess(res.data);
      } else {
        showToast(res.error || 'Failed to abort provider switch.', 'error');
      }
    } catch (err: any) {
      showToast(err.message || 'Error aborting provider switch.', 'error');
    } finally {
      setIsAborting(false);
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '16px', padding: '4px 0' }}>
      <div
        style={{
          background: 'rgba(234, 179, 8, 0.12)',
          border: '1px solid rgba(234, 179, 8, 0.35)',
          borderRadius: '8px',
          padding: '14px 16px',
          color: '#fde047',
          fontSize: '0.9rem',
          lineHeight: 1.5,
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', fontWeight: 600 }}>
          <span>⏳</span>
          <span>A provider migration is currently in progress.</span>
        </div>
        <div style={{ marginTop: '8px', color: 'var(--text-muted)', fontSize: '0.85rem' }}>
          AgentHub is preparing conversation handoff context and connecting to the target provider. It is strongly recommended to wait for this transition to complete.
        </div>
      </div>

      <div style={{ fontSize: '0.9rem', color: 'var(--text-main)', lineHeight: 1.5 }}>
        Do you want to abort the migration and revert back to your previous provider (<strong>{conversation.providerId}</strong>)?
      </div>

      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px', marginTop: '8px' }}>
        <button
          type="button"
          className="btn btn-secondary"
          onClick={onClose}
          disabled={isAborting}
        >
          Wait (Keep Migrating)
        </button>
        <button
          type="button"
          className="btn btn-danger"
          onClick={handleAbort}
          disabled={isAborting}
        >
          {isAborting ? 'Aborting...' : 'Abort & Revert'}
        </button>
      </div>
    </div>
  );
};
