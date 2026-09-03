import React, { useState, useEffect, useRef } from 'react';
import ReactDOM from 'react-dom';
import { apiFetch } from '../../services/apiClient';
import { FileChangeDto } from '../../types/diff';
import { useToast } from '../../context/ToastContext';
import { getChangeHunks, isCreatedDiff, isDeletedDiff } from './diff/diffViewerUtils';
import { DiffControlsBar } from './diff/DiffControlsBar';
import { DiffUnifiedView } from './diff/DiffUnifiedView';
import { DiffSideBySideView } from './diff/DiffSideBySideView';
import { DiffEditorView } from './diff/DiffEditorView';
import { DiffBinaryView } from './diff/DiffBinaryView';
import { DiffViewerFooter } from './diff/DiffViewerFooter';

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

    if (viewMode === 'sideBySide') {
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
      const performScroll = () => {
        const targetIndex = hunks[0].startIndex;
        if (viewMode === 'sideBySide') {
          const leftEl = document.getElementById(`diff-line-left-${targetIndex}`);
          const rightEl = document.getElementById(`diff-line-right-${targetIndex}`);
          leftEl?.scrollIntoView({ behavior: 'smooth', block: 'center' });
          rightEl?.scrollIntoView({ behavior: 'smooth', block: 'center' });
        } else {
          const rowEl = document.getElementById(`diff-line-row-${targetIndex}`);
          rowEl?.scrollIntoView({ behavior: 'smooth', block: 'center' });
        }
      };

      const rafId = requestAnimationFrame(performScroll);
      const timer = setTimeout(performScroll, 80);
      return () => {
        cancelAnimationFrame(rafId);
        clearTimeout(timer);
      };
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
          const initialId =
            initialFileChangeId && res.data.some((d) => d.id === initialFileChangeId)
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
        const itemFromList = changes.find((c) => c.id === activeChangeId);
        setActiveDiff({
          ...itemFromList,
          ...res.data,
          id: activeChangeId,
          changeType: res.data.changeType || itemFromList?.changeType || 'Modified',
        });
        setEditedContent(res.data.newContent || '');
        setIsEditing(false);
      }
    };
    fetchDiffDetail();
  }, [activeChangeId, workspaceId, changes]);

  const handleAccept = async () => {
    if (!activeChangeId) return;
    setIsProcessing(true);
    try {
      const payload =
        isEditing || (activeDiff && editedContent !== activeDiff.newContent)
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

  const handleRejectAll = async () => {
    setIsProcessing(true);
    try {
      const res = await apiFetch(
        `/api/v1/diffs/reject-all?conversationId=${conversationId}&workspaceId=${workspaceId}`,
        { method: 'POST' }
      );
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
  };

  const handleAcceptAll = async () => {
    setIsProcessing(true);
    try {
      const res = await apiFetch(`/api/v1/diffs/accept-all?conversationId=${conversationId}`, {
        method: 'POST',
      });
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
  };

  const isCreated = isCreatedDiff(activeDiff);
  const isDeleted = isDeletedDiff(activeDiff);

  if (isLoading) {
    return <div style={{ padding: '20px', textAlign: 'center', color: 'var(--text-muted)' }}>Loading diffs...</div>;
  }

  const modalContent = (
    <div className={`diff-modal-body ${isFullscreen ? 'diff-modal-fullscreen' : ''}`}>
      {/* Top controls / header bar */}
      {activeDiff && (
        <DiffControlsBar
          activeDiff={activeDiff}
          changes={changes}
          currentFileIndex={currentFileIndex}
          isFullscreen={isFullscreen}
          isEditing={isEditing}
          changeHunks={changeHunks}
          activeHunkIndex={activeHunkIndex}
          isWordWrap={isWordWrap}
          viewMode={viewMode}
          sideBySideMobileTab={sideBySideMobileTab}
          onPrevFile={handlePrevFile}
          onNextFile={handleNextFile}
          onSelectChange={setActiveChangeId}
          onPrevChange={handlePrevChange}
          onNextChange={handleNextChange}
          onToggleWordWrap={toggleWordWrap}
          onSetViewMode={setViewMode}
          onSetFullscreen={setIsFullscreen}
          onSetSideBySideMobileTab={setSideBySideMobileTab}
        />
      )}

      {/* Main diff container */}
      <div
        className={`diff-viewer-scroll-container ${isWordWrap ? 'diff-wrap-enabled' : 'diff-nowrap'}`}
        style={{
          flex: '1 1 auto',
          minHeight: '220px',
          height: isFullscreen ? 'calc(100dvh - 52px)' : '100%',
          overflow: 'auto',
          background: 'var(--bg-secondary)',
          padding: '12px',
          borderRadius: '8px',
          border: '1px solid var(--border-color)',
          marginBottom: isFullscreen ? '0' : '12px',
        }}
      >
        {!activeDiff ? (
          <p style={{ color: 'var(--text-muted)' }}>Loading diff details...</p>
        ) : activeDiff.isBinary ? (
          <DiffBinaryView activeDiff={activeDiff} />
        ) : viewMode === 'unified' ? (
          <DiffUnifiedView unifiedLines={activeDiff.unifiedLines || []} isWordWrap={isWordWrap} />
        ) : isEditing ? (
          <DiffEditorView
            isCreated={isCreated}
            editedContent={editedContent}
            isFullscreen={isFullscreen}
            isWordWrap={isWordWrap}
            onContentChange={setEditedContent}
            onExitEditing={() => setIsEditing(false)}
          />
        ) : (
          <DiffSideBySideView
            sideBySideLines={activeDiff.sideBySideLines || []}
            isCreated={isCreated}
            isDeleted={isDeleted}
            isMobile={isMobile}
            sideBySideMobileTab={sideBySideMobileTab}
            topPaneRef={topPaneRef}
            bottomPaneRef={bottomPaneRef}
            onTopScroll={handleTopScroll}
            onBottomScroll={handleBottomScroll}
            onStartEditing={() => setIsEditing(true)}
          />
        )}
      </div>

      {/* Footer action bar (Normal mode only) */}
      {!isFullscreen && (
        <DiffViewerFooter
          changeCount={changes.length}
          isEditing={isEditing}
          isProcessing={isProcessing}
          onRejectAll={handleRejectAll}
          onAcceptAll={handleAcceptAll}
          onReject={handleReject}
          onAccept={handleAccept}
        />
      )}
    </div>
  );

  if (isFullscreen && typeof document !== 'undefined') {
    return ReactDOM.createPortal(modalContent, document.body);
  }

  return modalContent;
};
