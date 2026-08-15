import React, { useState } from 'react';
import { apiFetch } from '../../services/apiClient';
import { InitializeSetupResponse } from '../../types/api';
import { useAuth } from '../../context/AuthContext';
import { useToast } from '../../context/ToastContext';

interface SetupWizardModalProps {
  onComplete: () => void;
}

export const SetupWizardModal: React.FC<SetupWizardModalProps> = ({ onComplete }) => {
  const { setIsSetupCompleted, setIsAuthenticated } = useAuth();
  const { showToast } = useToast();
  const [username, setUsername] = useState<string>('admin');
  const [password, setPassword] = useState<string>('');
  const [confirmPassword, setConfirmPassword] = useState<string>('');
  const [recoveryCode, setRecoveryCode] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!username.trim()) {
      showToast('Username is required.', 'error');
      return;
    }
    if (password.length < 6) {
      showToast('Password must be at least 6 characters.', 'error');
      return;
    }
    if (password !== confirmPassword) {
      showToast('Passwords do not match.', 'error');
      return;
    }

    setIsSubmitting(true);
    try {
      const res = await apiFetch<InitializeSetupResponse>('/api/v1/auth/setup/initialize', {
        method: 'POST',
        body: { username, password, confirmPassword },
      });

      if (res.ok && res.data) {
        setRecoveryCode(res.data.recoveryCode);
      } else {
        showToast((res.data as any)?.message || res.error || 'Setup failed.', 'error');
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleFinish = () => {
    setIsSetupCompleted(true);
    setIsAuthenticated(true);
    onComplete();
  };

  if (recoveryCode) {
    return (
      <div>
        <p style={{ color: '#f59e0b', marginBottom: '12px' }}>
          ⚠️ <strong>IMPORTANT:</strong> Save this recovery code securely. It is required to reset your
          administrator password if lost.
        </p>
        <div
          style={{
            background: 'rgba(0,0,0,0.5)',
            padding: '14px',
            fontFamily: 'var(--font-mono)',
            fontSize: '1.1rem',
            textAlign: 'center',
            borderRadius: '6px',
            letterSpacing: '2px',
            color: '#38bdf8',
            marginBottom: '16px',
          }}
        >
          {recoveryCode}
        </div>
        <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
          <button type="button" className="btn btn-primary" onClick={handleFinish}>
            Enter AI Agent Hub &rarr;
          </button>
        </div>
      </div>
    );
  }

  return (
    <form onSubmit={handleSubmit}>
      <p className="card-subtitle">
        Create the single administrator account. A cryptographically secure Master Key will be initialized.
      </p>
      <div className="form-group">
        <label className="form-label">Admin Username</label>
        <input
          type="text"
          className="form-input"
          value={username}
          onChange={(e) => setUsername(e.target.value)}
        />
      </div>
      <div className="form-group">
        <label className="form-label">Password</label>
        <input
          type="password"
          className="form-input"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          placeholder="At least 6 characters"
        />
      </div>
      <div className="form-group">
        <label className="form-label">Confirm Password</label>
        <input
          type="password"
          className="form-input"
          value={confirmPassword}
          onChange={(e) => setConfirmPassword(e.target.value)}
        />
      </div>
      <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: '16px' }}>
        <button type="submit" className="btn btn-primary" disabled={isSubmitting}>
          {isSubmitting ? 'Creating...' : 'Create Administrator Account →'}
        </button>
      </div>
    </form>
  );
};
