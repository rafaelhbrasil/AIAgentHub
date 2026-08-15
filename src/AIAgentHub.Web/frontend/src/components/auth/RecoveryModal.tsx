import React, { useState } from 'react';
import { apiFetch } from '../../services/apiClient';
import { useAuth } from '../../context/AuthContext';
import { useToast } from '../../context/ToastContext';

interface RecoveryModalProps {
  onResetSuccess: () => void;
  onCancel: () => void;
}

export const RecoveryModal: React.FC<RecoveryModalProps> = ({ onResetSuccess, onCancel }) => {
  const { canResetWithoutCode, setIsSetupCompleted, setIsAuthenticated, checkAuthAndSetup } = useAuth();
  const { showToast } = useToast();
  const [recoveryCode, setRecoveryCode] = useState<string>('');
  const [wipeStep, setWipeStep] = useState<0 | 1 | 2>(0);
  const [isProcessing, setIsProcessing] = useState<boolean>(false);

  const handleRecoverWithCode = async () => {
    if (!recoveryCode.trim()) {
      showToast('Please enter your recovery code.', 'error');
      return;
    }

    setIsProcessing(true);
    try {
      const res = await apiFetch('/api/v1/auth/recover', {
        method: 'POST',
        body: { recoveryCode: recoveryCode.trim() },
      });

      if (res.ok) {
        showToast('System reset to Setup Mode.', 'success');
        setIsSetupCompleted(false);
        setIsAuthenticated(false);
        onResetSuccess();
      } else {
        showToast(res.data?.message || res.error || 'Invalid recovery code.', 'error');
      }
    } finally {
      setIsProcessing(false);
    }
  };

  const handleExecuteWipe = async () => {
    setIsProcessing(true);
    try {
      const res = await apiFetch('/api/v1/auth/recover-wipe', { method: 'POST' });
      if (res.ok) {
        showToast('Database wiped and system reset to Setup Mode.', 'success');
        setIsSetupCompleted(false);
        setIsAuthenticated(false);
        await checkAuthAndSetup();
        onResetSuccess();
      } else {
        showToast(res.data?.message || res.error || 'Failed to wipe database.', 'error');
      }
    } finally {
      setIsProcessing(false);
    }
  };

  if (wipeStep === 1) {
    return (
      <div>
        <div style={{ textAlign: 'center', padding: '10px 0' }}>
          <span style={{ fontSize: '3rem' }}>⚠️</span>
          <h4 style={{ color: '#f87171', margin: '12px 0 6px 0', fontSize: '1.1rem' }}>
            FORCE DATA WIPE WARNING
          </h4>
          <p style={{ color: 'var(--text-muted)', fontSize: '0.9rem', lineHeight: 1.5 }}>
            This action will <strong>FORCEFULLY ERASE all database data</strong> including all workspaces,
            user accounts, conversations, messages, file changes, encrypted secrets, and server settings.
          </p>
          <p style={{ color: '#fca5a5', fontSize: '0.85rem', marginTop: '10px', fontWeight: 500 }}>
            This operation is permanent and CANNOT be undone.
          </p>
        </div>
        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px', marginTop: '16px' }}>
          <button type="button" className="btn btn-secondary" onClick={() => setWipeStep(0)}>
            Cancel
          </button>
          <button
            type="button"
            className="btn btn-danger"
            style={{ background: '#dc2626' }}
            onClick={() => setWipeStep(2)}
          >
            I Understand, Proceed &rarr;
          </button>
        </div>
      </div>
    );
  }

  if (wipeStep === 2) {
    return (
      <div>
        <div style={{ textAlign: 'center', padding: '10px 0' }}>
          <span style={{ fontSize: '3rem' }}>⛔</span>
          <h4 style={{ color: '#ef4444', margin: '12px 0 6px 0', fontSize: '1.1rem' }}>
            FINAL CONFIRMATION REQUIRED
          </h4>
          <p style={{ color: 'var(--text-muted)', fontSize: '0.9rem', lineHeight: 1.5 }}>
            Are you <strong>100% SURE</strong> you want to permanently erase the entire database?
          </p>
          <p style={{ color: '#f87171', fontSize: '0.85rem', marginTop: '10px', fontWeight: 600 }}>
            All data will be permanently purged and the system will return to Setup Mode.
          </p>
        </div>
        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px', marginTop: '16px' }}>
          <button type="button" className="btn btn-secondary" onClick={() => setWipeStep(0)}>
            Cancel
          </button>
          <button
            type="button"
            className="btn btn-danger"
            style={{ background: '#b91c1c', fontWeight: 700 }}
            onClick={handleExecuteWipe}
            disabled={isProcessing}
          >
            {isProcessing ? 'Purging...' : '⛔ CONFIRM DATA WIPE'}
          </button>
        </div>
      </div>
    );
  }

  return (
    <div>
      <p className="card-subtitle" style={{ marginBottom: '12px' }}>
        Enter your 16-character recovery code (e.g. XXXX-XXXX-XXXX-XXXX) to reset the system to Setup Mode.
      </p>
      <div className="form-group">
        <label className="form-label">Recovery Code</label>
        <input
          type="text"
          className="form-input"
          value={recoveryCode}
          onChange={(e) => setRecoveryCode(e.target.value)}
          placeholder="XXXX-XXXX-XXXX-XXXX"
        />
      </div>
      <div
        style={{
          marginTop: '14px',
          padding: '12px',
          background: 'rgba(255,255,255,0.04)',
          border: '1px solid var(--border-color)',
          borderRadius: '6px',
          fontSize: '0.8rem',
          color: 'var(--text-muted)',
          lineHeight: 1.4,
        }}
      >
        💡 <strong>Lost your recovery code?</strong> Restart the AI Agent Hub server with the{' '}
        <code>--recovery</code> command-line parameter. If accessed from localhost with <code>--recovery</code>{' '}
        enabled, an option will appear here to reset the system without a code (wiping all database data).
      </div>

      {canResetWithoutCode && (
        <div
          style={{
            marginTop: '14px',
            paddingTop: '12px',
            borderTop: '1px solid rgba(239, 68, 68, 0.3)',
          }}
        >
          <p style={{ color: '#f87171', fontSize: '0.82rem', marginBottom: '8px' }}>
            ⚠️ <strong>Localhost Emergency Reset Enabled (--recovery)</strong>
          </p>
          <button
            type="button"
            className="btn btn-warning"
            style={{
              width: '100%',
              fontSize: '0.85rem',
              background: 'rgba(239, 68, 68, 0.2)',
              border: '1px solid #ef4444',
              color: '#fca5a5',
            }}
            onClick={() => setWipeStep(1)}
          >
            ⚡ Reset System Without Code (Erase All Data)
          </button>
        </div>
      )}

      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px', marginTop: '16px' }}>
        <button type="button" className="btn btn-secondary" onClick={onCancel}>
          Cancel
        </button>
        <button
          type="button"
          className="btn btn-danger"
          onClick={handleRecoverWithCode}
          disabled={isProcessing}
        >
          {isProcessing ? 'Resetting...' : 'Reset System'}
        </button>
      </div>
    </div>
  );
};
