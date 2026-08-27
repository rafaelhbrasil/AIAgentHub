import React from 'react';

interface DiffViewerFooterProps {
  changeCount: number;
  isEditing: boolean;
  isProcessing: boolean;
  onRejectAll: () => void;
  onAcceptAll: () => void;
  onReject: () => void;
  onAccept: () => void;
}

export const DiffViewerFooter: React.FC<DiffViewerFooterProps> = ({
  changeCount,
  isEditing,
  isProcessing,
  onRejectAll,
  onAcceptAll,
  onReject,
  onAccept,
}) => {
  return (
    <div
      style={{
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        gap: '12px',
        flexWrap: 'wrap',
      }}
    >
      <div>
        {changeCount > 1 && (
          <div style={{ display: 'flex', gap: '8px' }}>
            <button
              type="button"
              className="btn btn-secondary compact-btn text-danger"
              onClick={onRejectAll}
              disabled={isProcessing}
            >
              Reject All ({changeCount})
            </button>
            <button
              type="button"
              className="btn btn-secondary compact-btn"
              onClick={onAcceptAll}
              disabled={isProcessing}
            >
              Accept All ({changeCount})
            </button>
          </div>
        )}
      </div>

      <div style={{ display: 'flex', gap: '10px' }}>
        <button
          type="button"
          className="btn btn-danger"
          onClick={onReject}
          disabled={isProcessing}
        >
          ❌ Reject & Rollback
        </button>
        <button
          type="button"
          className="btn btn-success"
          onClick={onAccept}
          disabled={isProcessing}
        >
          ✔️ {isEditing ? 'Save & Accept' : 'Accept Changes'}
        </button>
      </div>
    </div>
  );
};
