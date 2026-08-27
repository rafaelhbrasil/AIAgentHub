import React from 'react';

interface DiffEditorViewProps {
  isCreated: boolean;
  editedContent: string;
  isFullscreen: boolean;
  isWordWrap: boolean;
  onContentChange: (val: string) => void;
  onExitEditing: () => void;
}

export const DiffEditorView: React.FC<DiffEditorViewProps> = ({
  isCreated,
  editedContent,
  isFullscreen,
  isWordWrap,
  onContentChange,
  onExitEditing,
}) => {
  return (
    <div className="diff-sbs-edit-container">
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          marginBottom: '8px',
        }}
      >
        <div style={{ color: '#4ade80', fontWeight: 600, fontSize: '0.82rem' }}>
          {isCreated ? 'NEW FILE (CREATED)' : 'MODIFIED (CURRENT)'} (EDITING)
        </div>
        <button
          type="button"
          className="btn compact-btn btn-primary"
          style={{ padding: '2px 8px', fontSize: '0.72rem' }}
          onClick={onExitEditing}
          title="View synchronized diff comparison"
        >
          👁️ View Diff
        </button>
      </div>
      <textarea
        className="form-textarea diff-editable-textarea"
        value={editedContent}
        onChange={(e) => onContentChange(e.target.value)}
        style={{
          width: '100%',
          height: isFullscreen ? 'calc(100dvh - 120px)' : '46vh',
          minHeight: '260px',
          background: '#060a12',
          color: '#f1f5f9',
          fontFamily: 'var(--font-mono)',
          fontSize: '0.84rem',
          lineHeight: '1.45',
          border: '1px solid rgba(99, 102, 241, 0.4)',
          borderRadius: '4px',
          padding: '8px 10px',
          resize: 'vertical',
          whiteSpace: isWordWrap ? 'pre-wrap' : 'pre',
          overflowWrap: isWordWrap ? 'break-word' : 'normal',
          overflowX: isWordWrap ? 'hidden' : 'auto',
        }}
        placeholder="Edit file content..."
        spellCheck={false}
      />
    </div>
  );
};
