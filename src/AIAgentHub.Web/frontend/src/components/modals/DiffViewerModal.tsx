import React, { useState, useEffect } from 'react';
import { apiFetch } from '../../services/apiClient';
import { FileChangeDto, DiffChangeType } from '../../types/diff';
import { useToast } from '../../context/ToastContext';

interface DiffViewerModalProps {
  conversationId: string;
  workspaceId: string;
  onClose: () => void;
  onRefreshWorkspace?: () => void;
}

export const DiffViewerModal: React.FC<DiffViewerModalProps> = ({
  conversationId,
  workspaceId,
  onClose,
  onRefreshWorkspace,
}) => {
  const { showToast } = useToast();
  const [changes, setChanges] = useState<FileChangeDto[]>([]);
  const [activeChangeId, setActiveChangeId] = useState<string | null>(null);
  const [activeDiff, setActiveDiff] = useState<FileChangeDto | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [isProcessing, setIsProcessing] = useState<boolean>(false);

  useEffect(() => {
    const fetchChanges = async () => {
      setIsLoading(true);
      try {
        const res = await apiFetch<FileChangeDto[]>(`/api/v1/diffs?conversationId=${conversationId}`);
        if (res.ok && res.data && res.data.length > 0) {
          setChanges(res.data);
          setActiveChangeId(res.data[0].id);
        } else {
          setChanges([]);
          showToast('No file modifications recorded in this conversation.', 'info');
          onClose();
        }
      } finally {
        setIsLoading(false);
      }
    };
    fetchChanges();
  }, [conversationId]);

  useEffect(() => {
    if (!activeChangeId) return;
    const fetchDiffDetail = async () => {
      const res = await apiFetch<FileChangeDto>(`/api/v1/diffs/${activeChangeId}?workspaceId=${workspaceId}`);
      if (res.ok && res.data) {
        setActiveDiff(res.data);
      }
    };
    fetchDiffDetail();
  }, [activeChangeId, workspaceId]);

  const handleAccept = async () => {
    if (!activeChangeId) return;
    setIsProcessing(true);
    try {
      const res = await apiFetch(`/api/v1/diffs/${activeChangeId}/accept`, { method: 'POST' });
      if (res.ok) {
        showToast('Change marked as Accepted.', 'success');
        onClose();
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
        onClose();
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
    <div>
      <div style={{ display: 'flex', gap: '10px', marginBottom: '12px', overflowX: 'auto' }}>
        {changes.map((c) => (
          <button
            key={c.id}
            type="button"
            className={`btn btn-secondary ${activeChangeId === c.id ? 'btn-primary' : ''}`}
            onClick={() => setActiveChangeId(c.id)}
          >
            {c.relativePath} ({formatChangeType(c.changeType)})
          </button>
        ))}
      </div>

      <div
        style={{
          maxHeight: '55vh',
          overflow: 'auto',
          background: '#000',
          padding: '12px',
          borderRadius: '6px',
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
            <div className="diff-pane" style={{ borderRight: '1px solid var(--border-color)' }}>
              <div style={{ color: 'var(--text-muted)', marginBottom: '8px', fontWeight: 600 }}>ORIGINAL (BASELINE)</div>
              {(activeDiff.sideBySideLines || []).map((l, i) => (
                <div key={i} className={`diff-line ${l.leftKind === 2 ? 'deleted' : 'unchanged'}`}>
                  {l.leftLineNumber ? `${l.leftLineNumber.toString().padStart(4, ' ')} ` : '     '}
                  {l.leftText || ''}
                </div>
              ))}
            </div>
            <div className="diff-pane">
              <div style={{ color: '#6ee7b7', marginBottom: '8px', fontWeight: 600 }}>MODIFIED (CURRENT)</div>
              {(activeDiff.sideBySideLines || []).map((l, i) => (
                <div key={i} className={`diff-line ${l.rightKind === 1 ? 'added' : 'unchanged'}`}>
                  {l.rightLineNumber ? `${l.rightLineNumber.toString().padStart(4, ' ')} ` : '     '}
                  {l.rightText || ''}
                </div>
              ))}
            </div>
          </div>
        )}
      </div>

      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px' }}>
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
          ✔️ Accept Changes
        </button>
      </div>
    </div>
  );
};
