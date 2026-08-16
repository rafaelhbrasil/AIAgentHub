import React, { useState, useEffect } from 'react';
import { apiFetch } from '../../services/apiClient';
import { FileChangeDto, DiffChangeType } from '../../types/diff';
import { useToast } from '../../context/ToastContext';

interface DiffViewerModalProps {
  conversationId: string;
  workspaceId: string;
  initialFileChangeId?: string;
  onClose: () => void;
  onRefreshWorkspace?: () => void;
}

export const DiffViewerModal: React.FC<DiffViewerModalProps> = ({
  conversationId,
  workspaceId,
  initialFileChangeId,
  onClose,
  onRefreshWorkspace,
}) => {
  const { showToast } = useToast();
  const [changes, setChanges] = useState<FileChangeDto[]>([]);
  const [activeChangeId, setActiveChangeId] = useState<string | null>(null);
  const [activeDiff, setActiveDiff] = useState<FileChangeDto | null>(null);
  const [editedContent, setEditedContent] = useState<string>('');
  const [isEditing, setIsEditing] = useState<boolean>(false);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [isProcessing, setIsProcessing] = useState<boolean>(false);

  useEffect(() => {
    const fetchChanges = async () => {
      setIsLoading(true);
      try {
        const res = await apiFetch<FileChangeDto[]>(`/api/v1/diffs?conversationId=${conversationId}&pendingOnly=true`);
        if (res.ok && res.data && res.data.length > 0) {
          setChanges(res.data);
          const initialId = initialFileChangeId && res.data.some((d) => d.id === initialFileChangeId)
            ? initialFileChangeId
            : res.data[0].id;
          setActiveChangeId(initialId);
        } else {
          setChanges([]);
          showToast('No pending file modifications in this conversation.', 'info');
          onClose();
        }
      } finally {
        setIsLoading(false);
      }
    };
    fetchChanges();
  }, [conversationId, initialFileChangeId]);

  useEffect(() => {
    if (!activeChangeId) return;
    const fetchDiffDetail = async () => {
      const res = await apiFetch<FileChangeDto>(`/api/v1/diffs/${activeChangeId}?workspaceId=${workspaceId}`);
      if (res.ok && res.data) {
        setActiveDiff(res.data);
        setEditedContent(res.data.newContent || '');
        setIsEditing(false);
      }
    };
    fetchDiffDetail();
  }, [activeChangeId, workspaceId]);

  const handleAccept = async () => {
    if (!activeChangeId) return;
    setIsProcessing(true);
    try {
      const payload = isEditing || (activeDiff && editedContent !== activeDiff.newContent)
        ? { content: editedContent }
        : null;

      const res = await apiFetch(`/api/v1/diffs/${activeChangeId}/accept?workspaceId=${workspaceId}`, {
        method: 'POST',
        body: payload || undefined,
      });

      if (res.ok) {
        showToast('Change marked as Accepted.', 'success');
        const remaining = changes.filter((c) => c.id !== activeChangeId);
        setChanges(remaining);
        if (remaining.length > 0) {
          setActiveChangeId(remaining[0].id);
        } else {
          onClose();
        }
        onRefreshWorkspace?.();
      } else {
        showToast('Failed to accept change.', 'error');
      }
    } finally {
      setIsProcessing(false);
    }
  };

  const handleReject = async () => {
    if (!activeChangeId) return;
    setIsProcessing(true);
    try {
      const res = await apiFetch(`/api/v1/diffs/${activeChangeId}/reject?workspaceId=${workspaceId}`, {
        method: 'POST',
      });
      if (res.ok) {
        showToast('File rolled back to pre-execution snapshot.', 'success');
        const remaining = changes.filter((c) => c.id !== activeChangeId);
        setChanges(remaining);
        if (remaining.length > 0) {
          setActiveChangeId(remaining[0].id);
        } else {
          onClose();
        }
        onRefreshWorkspace?.();
      } else {
        showToast('Failed to rollback file.', 'error');
      }
    } finally {
      setIsProcessing(false);
    }
  };

  const formatChangeType = (type: DiffChangeType | number) => {
    if (type === DiffChangeType.Created || type === 1) return 'Created';
    if (type === DiffChangeType.Deleted || type === 2) return 'Deleted';
    return 'Modified';
  };

  if (isLoading) {
    return <div style={{ padding: '20px', textAlign: 'center', color: 'var(--text-muted)' }}>Loading diffs...</div>;
  }

  return (
    <div className="diff-modal-body">
      {/* File tabs */}
      <div style={{ display: 'flex', gap: '8px', marginBottom: '12px', overflowX: 'auto', paddingBottom: '4px' }}>
        {changes.map((c) => (
          <button
            key={c.id}
            type="button"
            className={`btn btn-secondary compact-btn ${activeChangeId === c.id ? 'btn-primary' : ''}`}
            onClick={() => setActiveChangeId(c.id)}
            style={{ fontSize: '0.8rem', padding: '4px 10px' }}
          >
            {c.relativePath} ({formatChangeType(c.changeType)})
          </button>
        ))}
      </div>

      {/* Main diff container */}
      <div
        className="diff-viewer-scroll-container"
        style={{
          maxHeight: '58vh',
          overflow: 'auto',
          background: '#090d16',
          padding: '12px',
          borderRadius: '8px',
          border: '1px solid var(--border-color)',
          marginBottom: '16px',
        }}
      >
        {!activeDiff ? (
          <p style={{ color: 'var(--text-muted)' }}>Loading diff details...</p>
        ) : activeDiff.isBinary ? (
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px' }}>
            <div>
              <strong>Original</strong>
              <br />
              <img src={activeDiff.oldContent || ''} alt="Original binary preview" style={{ maxWidth: '100%' }} />
            </div>
            <div>
              <strong>Modified</strong>
              <br />
              <img src={activeDiff.newContent || ''} alt="Modified binary preview" style={{ maxWidth: '100%' }} />
            </div>
          </div>
        ) : (
          <div className="diff-side-by-side">
            {/* Left Pane: Original */}
            <div className="diff-pane" style={{ borderRight: '1px solid var(--border-color)' }}>
              <div style={{ color: 'var(--text-muted)', marginBottom: '8px', fontWeight: 600, fontSize: '0.82rem' }}>
                ORIGINAL (BASELINE)
              </div>
              {(activeDiff.sideBySideLines || []).map((l, i) => (
                <div key={i} className={`diff-line ${l.leftKind === 2 ? 'deleted' : 'unchanged'}`}>
                  {l.leftLineNumber ? `${l.leftLineNumber.toString().padStart(4, ' ')} ` : '     '}
                  {l.leftText || ''}
                </div>
              ))}
            </div>

            {/* Right Pane: Modified / Editable */}
            <div className="diff-pane">
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
                <div style={{ color: '#6ee7b7', fontWeight: 600, fontSize: '0.82rem' }}>
                  MODIFIED (CURRENT) {isEditing ? '(EDITING)' : ''}
                </div>
                <button
                  type="button"
                  className={`btn compact-btn ${isEditing ? 'btn-primary' : 'btn-secondary'}`}
                  style={{ padding: '2px 8px', fontSize: '0.72rem' }}
                  onClick={() => setIsEditing((prev) => !prev)}
                  title="Toggle interactive editing mode for right pane"
                >
                  {isEditing ? '👁️ View Diff' : '✏️ Edit Content'}
                </button>
              </div>

              {isEditing ? (
                <textarea
                  className="form-textarea diff-editable-textarea"
                  value={editedContent}
                  onChange={(e) => setEditedContent(e.target.value)}
                  style={{
                    width: '100%',
                    height: '42vh',
                    minHeight: '240px',
                    background: '#060a12',
                    color: '#f1f5f9',
                    fontFamily: 'var(--font-mono)',
                    fontSize: '0.84rem',
                    lineHeight: '1.45',
                    border: '1px solid rgba(99, 102, 241, 0.4)',
                    borderRadius: '4px',
                    padding: '8px 10px',
                    resize: 'vertical',
                    whiteSpace: 'pre',
                    overflowWrap: 'normal',
                    overflowX: 'auto',
                  }}
                  placeholder="Edit file content..."
                  spellCheck={false}
                />
              ) : (
                (activeDiff.sideBySideLines || []).map((l, i) => (
                  <div key={i} className={`diff-line ${l.rightKind === 1 ? 'added' : 'unchanged'}`}>
                    {l.rightLineNumber ? `${l.rightLineNumber.toString().padStart(4, ' ')} ` : '     '}
                    {l.rightText || ''}
                  </div>
                ))
              )}
            </div>
          </div>
        )}
      </div>

      {/* Footer action bar */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: '12px', flexWrap: 'wrap' }}>
        <div>
          {changes.length > 1 && (
            <div style={{ display: 'flex', gap: '8px' }}>
              <button
                type="button"
                className="btn btn-secondary compact-btn text-danger"
                onClick={async () => {
                  setIsProcessing(true);
                  try {
                    const res = await apiFetch(`/api/v1/diffs/reject-all?conversationId=${conversationId}&workspaceId=${workspaceId}`, { method: 'POST' });
                    if (res.ok) {
                      showToast('All changes rejected and rolled back.', 'success');
                      onClose();
                      onRefreshWorkspace?.();
                    } else {
                      showToast('Failed to reject all changes.', 'error');
                    }
                  } finally {
                    setIsProcessing(false);
                  }
                }}
                disabled={isProcessing}
              >
                Reject All ({changes.length})
              </button>
              <button
                type="button"
                className="btn btn-secondary compact-btn"
                onClick={async () => {
                  setIsProcessing(true);
                  try {
                    const res = await apiFetch(`/api/v1/diffs/accept-all?conversationId=${conversationId}`, { method: 'POST' });
                    if (res.ok) {
                      showToast('All changes accepted.', 'success');
                      onClose();
                      onRefreshWorkspace?.();
                    } else {
                      showToast('Failed to accept all changes.', 'error');
                    }
                  } finally {
                    setIsProcessing(false);
                  }
                }}
                disabled={isProcessing}
              >
                Accept All ({changes.length})
              </button>
            </div>
          )}
        </div>

        <div style={{ display: 'flex', gap: '10px' }}>
          <button
            type="button"
            className="btn btn-danger"
            onClick={handleReject}
            disabled={isProcessing}
          >
            ❌ Reject & Rollback
          </button>
          <button
            type="button"
            className="btn btn-success"
            onClick={handleAccept}
            disabled={isProcessing}
          >
            ✔️ {isEditing ? 'Save & Accept' : 'Accept Changes'}
          </button>
        </div>
      </div>
    </div>
  );
};
