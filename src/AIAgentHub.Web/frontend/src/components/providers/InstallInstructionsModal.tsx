import React from 'react';
import { ProviderDto } from '../../types/provider';
import { useToast } from '../../context/ToastContext';

interface InstallInstructionsModalProps {
  provider: ProviderDto;
  onOpenDocUrl?: (url: string) => void;
  onClose: () => void;
}

export const InstallInstructionsModal: React.FC<InstallInstructionsModalProps> = ({
  provider,
  onOpenDocUrl,
  onClose,
}) => {
  const { showToast } = useToast();
  const installCmd = provider.installCommand || 'No specific command provided';
  const instructions =
    provider.installInstructions || 'Follow official provider documentation to complete installation.';
  const docUrl = provider.documentationUrl;

  const copyCommand = () => {
    navigator.clipboard.writeText(installCmd.trim());
    showToast('Installation command copied to clipboard.', 'success');
  };

  return (
    <div>
      <div style={{ marginBottom: '16px' }}>
        <div style={{ fontSize: '0.92rem', color: 'var(--text-muted)', marginBottom: '14px', lineHeight: 1.4 }}>
          {provider.description}
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '16px' }}>
          <span
            className="badge"
            style={{
              background: 'rgba(59, 130, 246, 0.15)',
              color: '#38bdf8',
              fontSize: '0.82rem',
              padding: '4px 10px',
              border: '1px solid rgba(56, 189, 248, 0.3)',
            }}
          >
            💻 OS Supported: Windows
          </span>
        </div>

        <div className="form-group" style={{ marginBottom: '16px' }}>
          <label className="form-label" style={{ fontWeight: 600, marginBottom: '6px', display: 'block', fontSize: '0.88rem' }}>
            Terminal Installation Command
          </label>
          <div
            style={{
              display: 'flex',
              gap: '8px',
              alignItems: 'center',
              background: 'rgba(0, 0, 0, 0.4)',
              border: '1px solid var(--border-color)',
              borderRadius: '6px',
              padding: '10px 14px',
            }}
          >
            <code style={{ flex: 1, fontFamily: 'monospace', fontSize: '0.9rem', color: '#38bdf8', wordBreak: 'break-all' }}>
              {installCmd}
            </code>
            <button
              type="button"
              className="btn btn-secondary"
              style={{ padding: '6px 12px', fontSize: '0.82rem', whiteSpace: 'nowrap' }}
              onClick={copyCommand}
            >
              📋 Copy
            </button>
          </div>
        </div>

        <div style={{ padding: '12px', background: 'rgba(255, 255, 255, 0.03)', border: '1px solid var(--border-color)', borderRadius: '6px' }}>
          <strong style={{ fontSize: '0.85rem', color: 'var(--text-heading)', display: 'block', marginBottom: '4px' }}>
            Instructions:
          </strong>
          <p style={{ fontSize: '0.88rem', color: 'var(--text-muted)', margin: 0, lineHeight: 1.4 }}>
            {instructions}
          </p>
        </div>
      </div>

      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px' }}>
        {docUrl && (
          <button
            type="button"
            className="btn btn-secondary"
            onClick={() => onOpenDocUrl?.(docUrl)}
          >
            🌐 Official Website / Repo
          </button>
        )}
        <button type="button" className="btn btn-primary" onClick={onClose}>
          Close
        </button>
      </div>
    </div>
  );
};
