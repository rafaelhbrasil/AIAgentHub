import React, { useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import { useModal } from '../../context/ModalContext';
import { useToast } from '../../context/ToastContext';
import { getSafeReturnUrl } from '../../utils/urlRouting';
import { SetupWizardModal } from './SetupWizardModal';
import { RecoveryModal } from './RecoveryModal';

export const SignInPage: React.FC = () => {
  const { login, isSetupCompleted } = useAuth();
  const { showModal, hideModal } = useModal();
  const { showToast } = useToast();
  const [username, setUsername] = useState<string>('admin');
  const [password, setPassword] = useState<string>('');
  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!username.trim()) {
      showToast('Username is required.', 'error');
      return;
    }

    setIsSubmitting(true);
    try {
      const result = await login(username, password);
      if (result.success) {
        // Resolve returnUrl from querystring or pathname
        const searchParams = new URLSearchParams(window.location.search);
        const queryReturnUrl = searchParams.get('returnUrl');
        const fallbackUrl = window.location.pathname !== '/login' ? (window.location.pathname + window.location.search) : null;
        const targetUrl = getSafeReturnUrl(queryReturnUrl) || getSafeReturnUrl(fallbackUrl) || '/';

        window.history.replaceState({}, '', targetUrl);
        window.dispatchEvent(new PopStateEvent('popstate'));
      } else {
        showToast(result.error || 'Login failed.', 'error');
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleOpenSetup = () => {
    showModal(
      'Initial Server Setup — Setup Mode',
      <SetupWizardModal onComplete={hideModal} />
    );
  };

  const handleOpenRecovery = () => {
    showModal(
      'Reset to Setup Mode using Recovery Code',
      <RecoveryModal
        onResetSuccess={() => {
          hideModal();
          handleOpenSetup();
        }}
        onCancel={hideModal}
      />
    );
  };

  return (
    <div className="auth-page-container">
      <div className="auth-card glass">
        <div className="auth-header">
          <h2>Sign In to AI Agent Hub</h2>
          <p>Enter your administrator credentials to proceed</p>
        </div>
        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label className="form-label">Username</label>
            <input
              type="text"
              id="loginUsername"
              className="form-input"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
            />
          </div>
          <div className="form-group">
            <label className="form-label">Password</label>
            <input
              type="password"
              id="loginPassword"
              className="form-input"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="Enter your password"
            />
          </div>
          <div style={{ marginTop: '20px', display: 'flex', flexDirection: 'column', gap: '12px' }}>
            <button
              type="submit"
              className="btn btn-primary"
              id="loginSubmitBtn"
              style={{ width: '100%', justifyContent: 'center' }}
              disabled={isSubmitting}
            >
              {isSubmitting ? 'Signing In...' : 'Sign In'}
            </button>
            <div
              style={{
                display: 'flex',
                justifyContent: 'space-between',
                alignItems: 'center',
                marginTop: '8px',
              }}
            >
              {!isSetupCompleted && (
                <button
                  type="button"
                  className="btn btn-secondary"
                  style={{ fontSize: '0.8rem' }}
                  onClick={handleOpenSetup}
                >
                  ⚡ Run Setup Wizard (Create Credentials)
                </button>
              )}
              {isSetupCompleted && (
                <a
                  href="#recover"
                  id="recoverLink"
                  style={{ fontSize: '0.8rem', color: '#818cf8', textDecoration: 'none' }}
                  onClick={(e) => {
                    e.preventDefault();
                    handleOpenRecovery();
                  }}
                >
                  Lost password? Enter Recovery Code
                </a>
              )}
            </div>
          </div>
        </form>
      </div>
    </div>
  );
};
