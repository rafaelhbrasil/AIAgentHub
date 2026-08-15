import React from 'react';

interface ExternalLinkDisclaimerModalProps {
  url: string;
  onProceed: () => void;
  onCancel: () => void;
}

export const ExternalLinkDisclaimerModal: React.FC<ExternalLinkDisclaimerModalProps> = ({
  url,
  onProceed,
  onCancel,
}) => {
  return (
    <div>
      <div style={{ marginBottom: '16px' }}>
        <p
          style={{
            fontSize: '0.95rem',
            color: 'var(--text-heading)',
            marginBottom: '12px',
            lineHeight: 1.5,
          }}
        >
          You are leaving <strong>AIAgentHub</strong> and being redirected to an external website:
        </p>
        <div
          style={{
            padding: '10px 14px',
            background: 'rgba(0, 0, 0, 0.4)',
            border: '1px solid var(--border-color)',
            borderRadius: '6px',
            wordBreak: 'break-all',
            marginBottom: '14px',
          }}
        >
          <code style={{ color: '#38bdf8', fontSize: '0.9rem' }}>{url}</code>
        </div>
        <p style={{ fontSize: '0.85rem', color: 'var(--text-muted)', margin: 0 }}>
          Please confirm if you want to leave this application to open the link in a new tab.
        </p>
      </div>
      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px' }}>
        <button type="button" className="btn btn-secondary" onClick={onCancel}>
          Cancel
        </button>
        <button type="button" className="btn btn-primary" onClick={onProceed}>
          Proceed to Site &rarr;
        </button>
      </div>
    </div>
  );
};
