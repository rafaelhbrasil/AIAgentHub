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
  const [viewMode, setViewMode] = useState<'sideBySide' | 'unified'>(() => {
    if (typeof window !== 'undefined' && window.innerWidth <= 768) {
      return 'unified';
    }
    return 'sideBySide';
  });
  const [isWordWrap, setIsWordWrap] = useState<boolean>(() => {
    if (typeof window !== 'undefined') {
      const saved = localStorage.getItem('agenthub_diff_word_wrap');
      if (saved !== null) return saved === 'true';
      return window.innerWidth <= 768;
    }
    return false;
  });
  const [sideBySideMobileTab, setSideBySideMobileTab] = useState<'modified' | 'original' | 'split'>('split');
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [isProcessing, setIsProcessing] = useState<boolean>(false);

  const toggleWordWrap = () => {
    setIsWordWrap((prev) => {
      const next = !prev;
      localStorage.setItem('agenthub_diff_word_wrap', String(next));
      return next;
    });
  };

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

  const formatChangeType = (type: DiffChangeType | string) => {
    if (type === DiffChangeType.Created || type === 'Created') return 'Created';
    if (type === DiffChangeType.Deleted || type === 'Deleted') return 'Deleted';
    return 'Modified';
  };

  const isCreated = activeDiff?.changeType === DiffChangeType.Created || (activeDiff?.changeType as any) === 'Created';
  const isDeleted = activeDiff?.changeType === DiffChangeType.Deleted || (activeDiff?.changeType as any) === 'Deleted';
  const isModified = !isCreated && !isDeleted;

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
            style={{ fontSize: '0.8rem', padding: '4px 10px', whiteSpace: 'nowrap' }}
            title={c.relativePath}
          >
            {c.relativePath} ({formatChangeType(c.changeType)})
          </button>
        ))}
      </div>

      {/* Diff controls & summary header */}
      {activeDiff && (
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px', flexWrap: 'wrap', gap: '8px' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '0.85rem', flexWrap: 'wrap' }}>
            <strong style={{ color: 'var(--text-heading)' }}>{activeDiff.relativePath}</strong>
            <span
              style={{
                fontSize: '0.72rem',
                padding: '1px 6px',
                borderRadius: '4px',
                background: isCreated ? 'rgba(74, 222, 128, 0.15)' : isDeleted ? 'rgba(248, 113, 113, 0.15)' : 'rgba(99, 102, 241, 0.15)',
                color: isCreated ? '#4ade80' : isDeleted ? '#f87171' : '#818cf8',
                fontWeight: 600,
              }}
            >
              {formatChangeType(activeDiff.changeType)}
            </span>
            {activeDiff.additionsCount !== undefined && activeDiff.additionsCount > 0 && (
              <span style={{ color: '#4ade80', fontWeight: 600, fontSize: '0.78rem' }}>+{activeDiff.additionsCount}</span>
            )}
            {activeDiff.deletionsCount !== undefined && activeDiff.deletionsCount > 0 && (
              <span style={{ color: '#f87171', fontWeight: 600, fontSize: '0.78rem' }}>-{activeDiff.deletionsCount}</span>
            )}
          </div>

          {!activeDiff.isBinary && (
            <div style={{ display: 'flex', gap: '6px', flexWrap: 'wrap', alignItems: 'center' }}>
              <button
                type="button"
                className={`btn compact-btn ${isWordWrap ? 'btn-primary' : 'btn-secondary'}`}
                style={{ padding: '2px 8px', fontSize: '0.74rem' }}
                onClick={toggleWordWrap}
                title="Toggle Word-Wrap / Line-Break for code lines"
              >
                {isWordWrap ? '↩ Wrap: ON' : '➡ Wrap: OFF'}
              </button>
              <button
                type="button"
                className={`btn compact-btn ${viewMode === 'sideBySide' ? 'btn-primary' : 'btn-secondary'}`}
                style={{ padding: '2px 8px', fontSize: '0.74rem' }}
                onClick={() => setViewMode('sideBySide')}
              >
                📖 Side-by-Side
              </button>
              <button
                type="button"
                className={`btn compact-btn ${viewMode === 'unified' ? 'btn-primary' : 'btn-secondary'}`}
                style={{ padding: '2px 8px', fontSize: '0.74rem' }}
                onClick={() => setViewMode('unified')}
              >
                📜 In-Line Diff
              </button>
            </div>
          )}
        </div>
      )}

      {/* Sub-view switcher for Mobile Side-by-Side modified files */}
      {activeDiff && !activeDiff.isBinary && viewMode === 'sideBySide' && isModified && (
        <div className="diff-mobile-subnav" style={{ display: 'flex', gap: '6px', marginBottom: '8px' }}>
          <button
            type="button"
            className={`btn compact-btn ${sideBySideMobileTab === 'modified' ? 'btn-primary' : 'btn-secondary'}`}
            style={{ flex: 1, padding: '2px 6px', fontSize: '0.72rem', justifyContent: 'center' }}
            onClick={() => setSideBySideMobileTab('modified')}
          >
            Modified (After)
          </button>
          <button
            type="button"
            className={`btn compact-btn ${sideBySideMobileTab === 'original' ? 'btn-primary' : 'btn-secondary'}`}
            style={{ flex: 1, padding: '2px 6px', fontSize: '0.72rem', justifyContent: 'center' }}
            onClick={() => setSideBySideMobileTab('original')}
          >
            Original (Before)
          </button>
          <button
            type="button"
            className={`btn compact-btn ${sideBySideMobileTab === 'split' ? 'btn-primary' : 'btn-secondary'}`}
            style={{ flex: 1, padding: '2px 6px', fontSize: '0.72rem', justifyContent: 'center' }}
            onClick={() => setSideBySideMobileTab('split')}
          >
            Split 50/50
          </button>
        </div>
      )}

      {/* Main diff container */}
      <div
        className={`diff-viewer-scroll-container ${isWordWrap ? 'diff-wrap-enabled' : 'diff-nowrap'}`}
        style={{
          flex: '1 1 auto',
          minHeight: '200px',
          maxHeight: 'calc(100vh - 220px)',
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
        ) : viewMode === 'unified' ? (
          <div className="diff-unified">
            {(activeDiff.unifiedLines || []).map((l, i) => {
              const isAdd = l.kind === 1 || (l.kind as any) === 'Added' || (l.kind as any) === 'Addition';
              const isDel = l.kind === 2 || (l.kind as any) === 'Deleted' || (l.kind as any) === 'Deletion';
              return (
                <div
                  key={i}
                  className={`diff-line ${isAdd ? 'added' : isDel ? 'deleted' : 'unchanged'}`}
                  style={{ display: 'flex', gap: '8px', minWidth: isWordWrap ? '0' : 'max-content' }}
                >
                  <span style={{ width: '36px', flexShrink: 0, textAlign: 'right', color: 'var(--text-muted)', userSelect: 'none', opacity: 0.6 }}>
                    {l.oldLineNumber || ''}
                  </span>
                  <span style={{ width: '36px', flexShrink: 0, textAlign: 'right', color: 'var(--text-muted)', userSelect: 'none', opacity: 0.6 }}>
                    {l.newLineNumber || ''}
                  </span>
                  <span style={{ width: '16px', flexShrink: 0, textAlign: 'center', userSelect: 'none', fontWeight: 600 }}>
                    {isAdd ? '+' : isDel ? '-' : ' '}
                  </span>
                  <span className="diff-line-text" style={{ flex: 1 }}>{l.content}</span>
                </div>
              );
            })}
          </div>
        ) : (
          <div className={`diff-side-by-side ${isCreated ? 'single-pane created-only' : isDeleted ? 'single-pane deleted-only' : `mobile-sub-${sideBySideMobileTab}`}`}>
            {/* Left Pane: Original (Hidden if file was created) */}
            {!isCreated && (sideBySideMobileTab !== 'modified' || isDeleted) && (
              <div className="diff-pane diff-pane-original" style={{ borderRight: !isDeleted ? '1px solid var(--border-color)' : 'none' }}>
                <div style={{ color: '#f87171', marginBottom: '8px', fontWeight: 600, fontSize: '0.82rem' }}>
                  {isDeleted ? 'DELETED FILE (ORIGINAL)' : 'ORIGINAL (BASELINE)'}
                </div>
                {(activeDiff.sideBySideLines || []).map((l, i) => {
                  const isDel = l.leftKind === 2 || (l.leftKind as any) === 'Deleted' || (l.leftKind as any) === 'Deletion';
                  return (
                    <div key={i} className={`diff-line ${isDel ? 'deleted' : 'unchanged'}`}>
                      <span className="diff-line-no" style={{ width: '38px', flexShrink: 0, display: 'inline-block', color: 'var(--text-muted)' }}>
                        {l.leftLineNumber || ''}
                      </span>
                      <span className="diff-line-text">{l.leftText || ''}</span>
                    </div>
                  );
                })}
              </div>
            )}

            {/* Right Pane: Modified / Editable (Hidden if file was deleted) */}
            {!isDeleted && (sideBySideMobileTab !== 'original' || isCreated) && (
              <div className="diff-pane diff-pane-modified">
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
                  <div style={{ color: '#4ade80', fontWeight: 600, fontSize: '0.82rem' }}>
                    {isCreated ? 'NEW FILE (CREATED)' : 'MODIFIED (CURRENT)'} {isEditing ? '(EDITING)' : ''}
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
                      whiteSpace: isWordWrap ? 'pre-wrap' : 'pre',
                      overflowWrap: isWordWrap ? 'break-word' : 'normal',
                      overflowX: isWordWrap ? 'hidden' : 'auto',
                    }}
                    placeholder="Edit file content..."
                    spellCheck={false}
                  />
                ) : (
                  (activeDiff.sideBySideLines || []).map((l, i) => {
                    const isAdd = l.rightKind === 1 || (l.rightKind as any) === 'Added' || (l.rightKind as any) === 'Addition';
                    return (
                      <div key={i} className={`diff-line ${isAdd ? 'added' : 'unchanged'}`}>
                        <span className="diff-line-no" style={{ width: '38px', flexShrink: 0, display: 'inline-block', color: 'var(--text-muted)' }}>
                          {l.rightLineNumber || ''}
                        </span>
                        <span className="diff-line-text">{l.rightText || ''}</span>
                      </div>
                    );
                  })
                )}
              </div>
            )}
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
