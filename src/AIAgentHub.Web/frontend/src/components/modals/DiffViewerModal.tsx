import React, { useState, useEffect, useRef } from 'react';
import { apiFetch } from '../../services/apiClient';
import { FileChangeDto, DiffChangeType } from '../../types/diff';
import { useToast } from '../../context/ToastContext';

interface ChangeHunk {
  id: number;
  startIndex: number;
  endIndex: number;
}

const getChangeHunks = (diff: FileChangeDto | null, mode: 'sideBySide' | 'unified'): ChangeHunk[] => {
  if (!diff) return [];
  const hunks: ChangeHunk[] = [];
  let currentStart: number | null = null;
  let currentEnd: number | null = null;

  if (mode === 'sideBySide' && diff.sideBySideLines) {
    diff.sideBySideLines.forEach((l, idx) => {
      const isChanged =
        l.leftKind === 1 ||
        l.leftKind === 2 ||
        (l.leftKind as any) === 'Deleted' ||
        (l.leftKind as any) === 'Deletion' ||
        l.rightKind === 1 ||
        l.rightKind === 2 ||
        (l.rightKind as any) === 'Added' ||
        (l.rightKind as any) === 'Addition' ||
        (l.leftLineNumber == null && l.rightLineNumber != null) ||
        (l.leftLineNumber != null && l.rightLineNumber == null);

      if (isChanged) {
        if (currentStart === null) {
          currentStart = idx;
        }
        currentEnd = idx;
      } else {
        if (currentStart !== null && currentEnd !== null) {
          hunks.push({ id: hunks.length + 1, startIndex: currentStart, endIndex: currentEnd });
          currentStart = null;
          currentEnd = null;
        }
      }
    });
  } else if (mode === 'unified' && diff.unifiedLines) {
    diff.unifiedLines.forEach((l, idx) => {
      const isChanged =
        l.kind === 1 ||
        l.kind === 2 ||
        (l.kind as any) === 'Added' ||
        (l.kind as any) === 'Addition' ||
        (l.kind as any) === 'Deleted' ||
        (l.kind as any) === 'Deletion';
      if (isChanged) {
        if (currentStart === null) {
          currentStart = idx;
        }
        currentEnd = idx;
      } else {
        if (currentStart !== null && currentEnd !== null) {
          hunks.push({ id: hunks.length + 1, startIndex: currentStart, endIndex: currentEnd });
          currentStart = null;
          currentEnd = null;
        }
      }
    });
  }

  if (currentStart !== null && currentEnd !== null) {
    hunks.push({ id: hunks.length + 1, startIndex: currentStart, endIndex: currentEnd });
  }

  return hunks;
};

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
  const [isMobile, setIsMobile] = useState<boolean>(() => {
    return typeof window !== 'undefined' && window.innerWidth <= 768;
  });
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
  const [activeHunkIndex, setActiveHunkIndex] = useState<number>(-1);
  const [isFullscreen, setIsFullscreen] = useState<boolean>(false);

  const topPaneRef = useRef<HTMLDivElement>(null);
  const bottomPaneRef = useRef<HTMLDivElement>(null);
  const isSyncingScroll = useRef<boolean>(false);

  const changeHunks = React.useMemo(() => {
    return getChangeHunks(activeDiff, viewMode);
  }, [activeDiff, viewMode]);

  const currentFileIndex = changes.findIndex((c) => c.id === activeChangeId);

  const handlePrevFile = () => {
    if (currentFileIndex > 0) {
      setActiveChangeId(changes[currentFileIndex - 1].id);
    }
  };

  const handleNextFile = () => {
    if (currentFileIndex < changes.length - 1) {
      setActiveChangeId(changes[currentFileIndex + 1].id);
    }
  };

  const scrollToHunk = (hunkIndex: number) => {
    if (!changeHunks.length || hunkIndex < 0 || hunkIndex >= changeHunks.length) return;
    const targetIndex = changeHunks[hunkIndex].startIndex;

    if (isMobile && viewMode === 'sideBySide' && sideBySideMobileTab === 'split') {
      const leftEl = document.getElementById(`diff-line-left-${targetIndex}`);
      const rightEl = document.getElementById(`diff-line-right-${targetIndex}`);
      leftEl?.scrollIntoView({ behavior: 'smooth', block: 'center' });
      rightEl?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    } else {
      const rowEl = document.getElementById(`diff-line-row-${targetIndex}`);
      rowEl?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }
  };

  // Auto-scroll to first diff when opening dialog or switching files / viewModes
  useEffect(() => {
    if (!activeDiff || isEditing) return;
    const hunks = getChangeHunks(activeDiff, viewMode);
    if (hunks.length > 0) {
      setActiveHunkIndex(0);
      const timer = setTimeout(() => {
        const targetIndex = hunks[0].startIndex;
        if (isMobile && viewMode === 'sideBySide' && sideBySideMobileTab === 'split') {
          const leftEl = document.getElementById(`diff-line-left-${targetIndex}`);
          const rightEl = document.getElementById(`diff-line-right-${targetIndex}`);
          leftEl?.scrollIntoView({ behavior: 'smooth', block: 'center' });
          rightEl?.scrollIntoView({ behavior: 'smooth', block: 'center' });
        } else {
          const rowEl = document.getElementById(`diff-line-row-${targetIndex}`);
          rowEl?.scrollIntoView({ behavior: 'smooth', block: 'center' });
        }
      }, 120);
      return () => clearTimeout(timer);
    } else {
      setActiveHunkIndex(-1);
    }
  }, [activeDiff?.id, viewMode, sideBySideMobileTab, isEditing, isMobile]);

  const handlePrevChange = () => {
    if (changeHunks.length === 0) return;
    const prev = activeHunkIndex > 0 ? activeHunkIndex - 1 : changeHunks.length - 1;
    setActiveHunkIndex(prev);
    scrollToHunk(prev);
  };

  const handleNextChange = () => {
    if (changeHunks.length === 0) return;
    const next = activeHunkIndex < changeHunks.length - 1 ? activeHunkIndex + 1 : 0;
    setActiveHunkIndex(next);
    scrollToHunk(next);
  };

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && isFullscreen) {
        e.preventDefault();
        setIsFullscreen(false);
        return;
      }
      if (isEditing) return;
      if (e.key === 'F7') {
        e.preventDefault();
        if (e.shiftKey) {
          handlePrevChange();
        } else {
          handleNextChange();
        }
      } else if (e.altKey && e.key === 'ArrowDown') {
        e.preventDefault();
        handleNextChange();
      } else if (e.altKey && e.key === 'ArrowUp') {
        e.preventDefault();
        handlePrevChange();
      } else if (e.altKey && e.key === 'ArrowLeft') {
        e.preventDefault();
        handlePrevFile();
      } else if (e.altKey && e.key === 'ArrowRight') {
        e.preventDefault();
        handleNextFile();
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isEditing, changeHunks, activeHunkIndex, changes, activeChangeId, isFullscreen]);

  const handleTopScroll = (e: React.UIEvent<HTMLDivElement>) => {
    if (isSyncingScroll.current) return;
    if (!bottomPaneRef.current) return;
    isSyncingScroll.current = true;
    bottomPaneRef.current.scrollTop = e.currentTarget.scrollTop;
    bottomPaneRef.current.scrollLeft = e.currentTarget.scrollLeft;
    requestAnimationFrame(() => {
      isSyncingScroll.current = false;
    });
  };

  const handleBottomScroll = (e: React.UIEvent<HTMLDivElement>) => {
    if (isSyncingScroll.current) return;
    if (!topPaneRef.current) return;
    isSyncingScroll.current = true;
    topPaneRef.current.scrollTop = e.currentTarget.scrollTop;
    topPaneRef.current.scrollLeft = e.currentTarget.scrollLeft;
    requestAnimationFrame(() => {
      isSyncingScroll.current = false;
    });
  };

  useEffect(() => {
    const handleResize = () => {
      setIsMobile(window.innerWidth <= 768);
    };
    window.addEventListener('resize', handleResize);
    return () => window.removeEventListener('resize', handleResize);
  }, []);

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
    <div className={`diff-modal-body ${isFullscreen ? 'diff-modal-fullscreen' : ''}`}>
      {/* Fullscreen minimal top navigation bar */}
      {isFullscreen && activeDiff && (
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '6px', padding: '4px 8px', background: '#080c14', borderRadius: '6px', border: '1px solid var(--border-color)', flexShrink: 0 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px', overflow: 'hidden' }}>
            {changes.length > 1 && (
              <div style={{ display: 'flex', gap: '4px', flexShrink: 0 }}>
                <button
                  type="button"
                  className="btn btn-secondary compact-btn"
                  disabled={currentFileIndex <= 0}
                  onClick={handlePrevFile}
                  title="Previous File (Alt+Left)"
                  style={{ padding: '2px 6px', fontSize: '0.74rem' }}
                >
                  ◀
                </button>
                <button
                  type="button"
                  className="btn btn-secondary compact-btn"
                  disabled={currentFileIndex >= changes.length - 1}
                  onClick={handleNextFile}
                  title="Next File (Alt+Right)"
                  style={{ padding: '2px 6px', fontSize: '0.74rem' }}
                >
                  ▶
                </button>
              </div>
            )}
            <strong style={{ color: 'var(--text-heading)', fontSize: '0.84rem', whiteSpace: 'nowrap', textOverflow: 'ellipsis', overflow: 'hidden' }}>
              {activeDiff.relativePath}
            </strong>
          </div>

          <div style={{ display: 'flex', gap: '6px', alignItems: 'center', flexShrink: 0 }}>
            {changeHunks.length > 0 && !isEditing && (
              <div style={{ display: 'inline-flex', alignItems: 'center', gap: '2px', background: 'rgba(255, 255, 255, 0.05)', borderRadius: '6px', padding: '2px 4px', border: '1px solid var(--border-color)' }}>
                <button
                  type="button"
                  className="btn compact-btn btn-secondary"
                  style={{ padding: '2px 6px', fontSize: '0.74rem' }}
                  onClick={handlePrevChange}
                  title="Previous Change (Shift+F7 or Alt+Up)"
                >
                  ▲ Prev
                </button>
                <span style={{ fontSize: '0.74rem', color: 'var(--text-muted)', fontFamily: 'var(--font-mono)', minWidth: '60px', textAlign: 'center' }}>
                  {activeHunkIndex >= 0 ? `${activeHunkIndex + 1} / ${changeHunks.length}` : `${changeHunks.length} diffs`}
                </span>
                <button
                  type="button"
                  className="btn compact-btn btn-secondary"
                  style={{ padding: '2px 6px', fontSize: '0.74rem' }}
                  onClick={handleNextChange}
                  title="Next Change (F7 or Alt+Down)"
                >
                  ▼ Next
                </button>
              </div>
            )}
            <button
              type="button"
              className="btn compact-btn btn-primary"
              style={{ padding: '3px 10px', fontSize: '0.74rem' }}
              onClick={() => setIsFullscreen(false)}
              title="Close Full Screen (Escape)"
            >
              ✕ Close
            </button>
          </div>
        </div>
      )}

      {/* File tabs with previous / next file navigation (Normal mode only) */}
      {!isFullscreen && (
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '12px', overflowX: 'auto', paddingBottom: '4px' }}>
          {changes.length > 1 && (
            <div style={{ display: 'flex', gap: '4px', flexShrink: 0 }}>
              <button
                type="button"
                className="btn btn-secondary compact-btn"
                disabled={currentFileIndex <= 0}
                onClick={handlePrevFile}
                title="Previous File (Alt+Left)"
                style={{ padding: '4px 8px', fontSize: '0.78rem' }}
              >
                ◀
              </button>
              <button
                type="button"
                className="btn btn-secondary compact-btn"
                disabled={currentFileIndex >= changes.length - 1}
                onClick={handleNextFile}
                title="Next File (Alt+Right)"
                style={{ padding: '4px 8px', fontSize: '0.78rem' }}
              >
                ▶
              </button>
            </div>
          )}
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
      )}

      {/* Diff controls & summary header (Normal mode only) */}
      {!isFullscreen && activeDiff && (
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
              {changeHunks.length > 0 && !isEditing && (
                <div style={{ display: 'inline-flex', alignItems: 'center', gap: '2px', background: 'rgba(255, 255, 255, 0.05)', borderRadius: '6px', padding: '2px 4px', border: '1px solid var(--border-color)' }}>
                  <button
                    type="button"
                    className="btn compact-btn btn-secondary"
                    style={{ padding: '2px 6px', fontSize: '0.74rem' }}
                    onClick={handlePrevChange}
                    title="Previous Change (Shift+F7 or Alt+Up)"
                  >
                    ▲ Prev
                  </button>
                  <span style={{ fontSize: '0.74rem', color: 'var(--text-muted)', fontFamily: 'var(--font-mono)', minWidth: '64px', textAlign: 'center' }}>
                    {activeHunkIndex >= 0 ? `${activeHunkIndex + 1} / ${changeHunks.length}` : `${changeHunks.length} diffs`}
                  </span>
                  <button
                    type="button"
                    className="btn compact-btn btn-secondary"
                    style={{ padding: '2px 6px', fontSize: '0.74rem' }}
                    onClick={handleNextChange}
                    title="Next Change (F7 or Alt+Down)"
                  >
                    ▼ Next
                  </button>
                </div>
              )}
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
              <button
                type="button"
                className="btn compact-btn btn-secondary"
                style={{ padding: '2px 8px', fontSize: '0.74rem' }}
                onClick={() => setIsFullscreen(true)}
                title="Full Screen View"
              >
                ⛶ Full Screen
              </button>
            </div>
          )}
        </div>
      )}

      {/* Sub-view switcher for Mobile Side-by-Side modified files */}
      {!isFullscreen && activeDiff && !activeDiff.isBinary && viewMode === 'sideBySide' && isModified && (
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
          maxHeight: isFullscreen ? 'calc(100dvh - 52px)' : 'calc(100vh - 220px)',
          overflow: 'auto',
          background: '#090d16',
          padding: '12px',
          borderRadius: '8px',
          border: '1px solid var(--border-color)',
          marginBottom: isFullscreen ? '0' : '16px',
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
                  id={`diff-line-row-${i}`}
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
        ) : isEditing ? (
          <div className="diff-sbs-edit-container">
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
              <div style={{ color: '#4ade80', fontWeight: 600, fontSize: '0.82rem' }}>
                {isCreated ? 'NEW FILE (CREATED)' : 'MODIFIED (CURRENT)'} (EDITING)
              </div>
              <button
                type="button"
                className="btn compact-btn btn-primary"
                style={{ padding: '2px 8px', fontSize: '0.72rem' }}
                onClick={() => setIsEditing(false)}
                title="View synchronized diff comparison"
              >
                👁️ View Diff
              </button>
            </div>
            <textarea
              className="form-textarea diff-editable-textarea"
              value={editedContent}
              onChange={(e) => setEditedContent(e.target.value)}
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
        ) : isMobile && sideBySideMobileTab === 'split' && !isCreated && !isDeleted ? (
          <div className="diff-mobile-stacked-split">
            {/* Top Pane: Original Baseline (BEFORE) */}
            <div
              ref={topPaneRef}
              onScroll={handleTopScroll}
              className="diff-mobile-split-pane"
            >
              <div className="diff-pane-sticky-header">
                <span style={{ color: '#f87171', fontWeight: 600, fontSize: '0.82rem' }}>
                  ORIGINAL (BASELINE)
                </span>
              </div>
              <div className="diff-pane-lines">
                {(activeDiff.sideBySideLines || []).map((l, i) => {
                  const isDel = l.leftKind === 2 || (l.leftKind as any) === 'Deleted' || (l.leftKind as any) === 'Deletion';
                  const isLeftEmpty = l.leftLineNumber == null;
                  return (
                    <div
                      key={i}
                      id={`diff-line-left-${i}`}
                      className={`diff-line ${isDel ? 'deleted' : isLeftEmpty ? 'empty' : 'unchanged'}`}
                    >
                      <span className="diff-line-no">{l.leftLineNumber ?? ''}</span>
                      <span className="diff-line-marker">{isDel ? '-' : ' '}</span>
                      <span className="diff-line-text">{l.leftText ?? ''}</span>
                    </div>
                  );
                })}
              </div>
            </div>

            {/* Bottom Pane: Modified Current (AFTER) */}
            <div
              ref={bottomPaneRef}
              onScroll={handleBottomScroll}
              className="diff-mobile-split-pane"
            >
              <div className="diff-pane-sticky-header" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <span style={{ color: '#4ade80', fontWeight: 600, fontSize: '0.82rem' }}>
                  MODIFIED (CURRENT)
                </span>
                <button
                  type="button"
                  className="btn compact-btn btn-secondary"
                  style={{ padding: '2px 8px', fontSize: '0.72rem' }}
                  onClick={() => setIsEditing(true)}
                  title="Toggle interactive editing mode for right pane"
                >
                  ✏️ Edit Content
                </button>
              </div>
              <div className="diff-pane-lines">
                {(activeDiff.sideBySideLines || []).map((l, i) => {
                  const isAdd = l.rightKind === 1 || (l.rightKind as any) === 'Added' || (l.rightKind as any) === 'Addition';
                  const isRightEmpty = l.rightLineNumber == null;
                  return (
                    <div
                      key={i}
                      id={`diff-line-right-${i}`}
                      className={`diff-line ${isAdd ? 'added' : isRightEmpty ? 'empty' : 'unchanged'}`}
                    >
                      <span className="diff-line-no">{l.rightLineNumber ?? ''}</span>
                      <span className="diff-line-marker">{isAdd ? '+' : ' '}</span>
                      <span className="diff-line-text">{l.rightText ?? ''}</span>
                    </div>
                  );
                })}
              </div>
            </div>
          </div>
        ) : (
          <div className={`diff-sbs-table ${isCreated ? 'single-pane created-only' : isDeleted ? 'single-pane deleted-only' : `mobile-sub-${sideBySideMobileTab}`}`}>
            {/* Header row with sticky titles and Edit button */}
            <div className="diff-sbs-header-row">
              {!isCreated && (sideBySideMobileTab !== 'modified' || isDeleted) && (
                <div className="diff-sbs-header-cell header-original">
                  <span style={{ color: '#f87171', fontWeight: 600, fontSize: '0.82rem' }}>
                    {isDeleted ? 'DELETED FILE (ORIGINAL)' : 'ORIGINAL (BASELINE)'}
                  </span>
                </div>
              )}
              {!isDeleted && (sideBySideMobileTab !== 'original' || isCreated) && (
                <div className="diff-sbs-header-cell header-modified">
                  <span style={{ color: '#4ade80', fontWeight: 600, fontSize: '0.82rem' }}>
                    {isCreated ? 'NEW FILE (CREATED)' : 'MODIFIED (CURRENT)'}
                  </span>
                  <button
                    type="button"
                    className="btn compact-btn btn-secondary"
                    style={{ padding: '2px 8px', fontSize: '0.72rem' }}
                    onClick={() => setIsEditing(true)}
                    title="Toggle interactive editing mode for right pane"
                  >
                    ✏️ Edit Content
                  </button>
                </div>
              )}
            </div>

            {/* Synchronized row-by-row diff alignment */}
            <div className="diff-sbs-body">
              {(activeDiff.sideBySideLines || []).map((l, i) => {
                const isDel = l.leftKind === 2 || (l.leftKind as any) === 'Deleted' || (l.leftKind as any) === 'Deletion';
                const isAdd = l.rightKind === 1 || (l.rightKind as any) === 'Added' || (l.rightKind as any) === 'Addition';
                const isLeftEmpty = l.leftLineNumber == null;
                const isRightEmpty = l.rightLineNumber == null;

                return (
                  <div key={i} id={`diff-line-row-${i}`} className="diff-sbs-row">
                    {/* Left Cell */}
                    {!isCreated && (sideBySideMobileTab !== 'modified' || isDeleted) && (
                      <div className={`diff-sbs-cell diff-cell-left ${isDel ? 'deleted' : isLeftEmpty ? 'empty' : 'unchanged'}`}>
                        <span className="diff-line-no">{l.leftLineNumber ?? ''}</span>
                        <span className="diff-line-marker">{isDel ? '-' : ' '}</span>
                        <span className="diff-line-text">{l.leftText ?? ''}</span>
                      </div>
                    )}

                    {/* Right Cell */}
                    {!isDeleted && (sideBySideMobileTab !== 'original' || isCreated) && (
                      <div className={`diff-sbs-cell diff-cell-right ${isAdd ? 'added' : isRightEmpty ? 'empty' : 'unchanged'}`}>
                        <span className="diff-line-no">{l.rightLineNumber ?? ''}</span>
                        <span className="diff-line-marker">{isAdd ? '+' : ' '}</span>
                        <span className="diff-line-text">{l.rightText ?? ''}</span>
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          </div>
        )}
      </div>

      {/* Footer action bar (Normal mode only) */}
      {!isFullscreen && (
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
      )}
    </div>
  );
};
