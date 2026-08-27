import React from 'react';
import { ProviderDto } from '../../types/provider';

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
  const instructions =
    provider.installInstructions || 'Follow official provider documentation to complete installation.';
  const docUrl = provider.documentationUrl;

  return (
    <div>
      <div style={{ marginBottom: '20px' }}>
        <div style={{ fontSize: '0.92rem', color: 'var(--text-muted)', marginBottom: '16px', lineHeight: 1.5 }}>
          {provider.description}
        </div>

        <div style={{ padding: '14px', background: 'rgba(255, 255, 255, 0.03)', border: '1px solid var(--border-color)', borderRadius: '6px' }}>
          <strong style={{ fontSize: '0.85rem', color: 'var(--text-heading)', display: 'block', marginBottom: '6px' }}>
            Instructions:
          </strong>
          <p style={{ fontSize: '0.88rem', color: 'var(--text-muted)', margin: 0, lineHeight: 1.5 }}>
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
