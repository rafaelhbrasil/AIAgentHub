import React, { useState, useEffect, useCallback } from 'react';
import { apiFetch } from '../../../../services/apiClient';
import { GitStatusDto, GitBranchDto, GitCommitDto } from '../../../../types/git';
import { FileChangeDto } from '../../../../types/diff';
import { useToast } from '../../../../context/ToastContext';
import { useModal } from '../../../../context/ModalContext';
import { GitCommitLogTable } from './GitCommitLogTable';
import { GitWorkingTreeInspector } from './GitWorkingTreeInspector';
import { GitCommitDetailInspector } from './GitCommitDetailInspector';
import { DiffViewerModal } from '../../../modals/DiffViewerModal';
import { Spinner } from '../../../common/Spinner';

export interface GitWorkbenchProps {
  workspaceId: string;
  onFileSelect?: (path: string) => void;
  initialStatus?: GitStatusDto;
  initialBranches?: GitBranchDto[];
  initialHistory?: GitCommitDto[];
}

export const GitWorkbench: React.FC<GitWorkbenchProps> = ({
  workspaceId,
  onFileSelect,
  initialStatus,
  initialBranches,
  initialHistory,
}) => {
  const { showToast } = useToast();
  const { showModal, hideModal } = useModal();

  const [status, setStatus] = useState<GitStatusDto | null>(initialStatus || null);
  const [branches, setBranches] = useState<GitBranchDto[]>(initialBranches || []);
  const [history, setHistory] = useState<GitCommitDto[]>(initialHistory || []);
  const [selectedRowId, setSelectedRowId] = useState<string>('working-tree');
  const [commitMessage, setCommitMessage] = useState<string>('');
  const [isLoading, setIsLoading] = useState<boolean>(!initialStatus);
  const [isExecuting, setIsExecuting] = useState<boolean>(false);
  const [diffFileChange, setDiffFileChange] = useState<FileChangeDto | null>(null);
  const [splitRatio, setSplitRatio] = useState<number>(50); // percentage for top pane

  const fetchStatus = useCallback(async () => {
    try {
      const res = await apiFetch<GitStatusDto>(`/api/v1/git/status?workspaceId=${workspaceId}`);
      if (res.ok && res.data) {
        setStatus(res.data);
      }
    } catch {
      // Ignore
    }
  }, [workspaceId]);

  const fetchBranches = useCallback(async () => {
    try {
      const res = await apiFetch<GitBranchDto[]>(`/api/v1/git/branches?workspaceId=${workspaceId}`);
      if (res.ok && res.data) {
        setBranches(res.data);
      }
    } catch {
      // Ignore
    }
  }, [workspaceId]);

  const fetchHistory = useCallback(async () => {
    try {
      const res = await apiFetch<GitCommitDto[]>(`/api/v1/git/history?workspaceId=${workspaceId}&maxCount=50`);
      if (res.ok && res.data) {
        setHistory(res.data);
      }
    } catch {
      // Ignore
    }
  }, [workspaceId]);

  const refreshAll = useCallback(async () => {
    setIsLoading(true);
    try {
      await Promise.all([fetchStatus(), fetchBranches(), fetchHistory()]);
    } finally {
      setIsLoading(false);
    }
  }, [fetchStatus, fetchBranches, fetchHistory]);

  useEffect(() => {
    if (initialStatus) return;
    if (workspaceId) {
      refreshAll();
    }
  }, [workspaceId, refreshAll, initialStatus]);

  // Branch switching & creation
  const handleCheckoutBranch = async (branchName: string) => {
    setIsExecuting(true);
    try {
      const res = await apiFetch('/api/v1/git/checkout', {
        method: 'POST',
        body: { workspaceId, branchName },
      });
      if (res.ok) {
        showToast(`Switched to branch "${branchName}".`, 'success');
        refreshAll();
      } else {
        showToast((res.data as any)?.message || res.error || 'Failed to checkout branch.', 'error');
      }
    } finally {
      setIsExecuting(false);
    }
  };

  const handleCreateBranch = () => {
    showModal(
      'Create New Git Branch',
      <CreateBranchModal
        onSubmit={async (branchName) => {
          setIsExecuting(true);
          try {
            const res = await apiFetch('/api/v1/git/branch', {
              method: 'POST',
              body: { workspaceId, branchName, checkout: true },
            });
            if (res.ok) {
              showToast(`Created and checked out branch "${branchName}".`, 'success');
              hideModal();
              refreshAll();
            } else {
              showToast((res.data as any)?.message || res.error || 'Failed to create branch.', 'error');
            }
          } finally {
            setIsExecuting(false);
          }
        }}
        onCancel={hideModal}
      />
    );
  };

  // Remote operations
  const handleFetch = async () => {
    setIsExecuting(true);
    try {
      const res = await apiFetch('/api/v1/git/fetch', {
        method: 'POST',
        body: { workspaceId },
      });
      if (res.ok) {
        showToast('Fetched latest remote refs.', 'success');
        refreshAll();
      } else {
        showToast((res.data as any)?.message || res.error || 'Failed to fetch.', 'error');
      }
    } finally {
      setIsExecuting(false);
    }
  };

  const handlePull = async () => {
    setIsExecuting(true);
    try {
      const res = await apiFetch('/api/v1/git/pull', {
        method: 'POST',
        body: { workspaceId },
      });
      if (res.ok) {
        showToast('Pulled latest changes from remote.', 'success');
        refreshAll();
      } else {
        showToast((res.data as any)?.message || res.error || 'Failed to pull.', 'error');
      }
    } finally {
      setIsExecuting(false);
    }
  };

  const handlePush = async () => {
    setIsExecuting(true);
    try {
      const res = await apiFetch('/api/v1/git/push', {
        method: 'POST',
        body: { workspaceId },
      });
      if (res.ok) {
        showToast('Pushed commits to remote.', 'success');
        refreshAll();
      } else {
        showToast((res.data as any)?.message || res.error || 'Failed to push.', 'error');
      }
    } finally {
      setIsExecuting(false);
    }
  };

  // Working tree stage/unstage/commit/stash
  const handleStage = async (files?: string[], stageAll = false) => {
    setIsExecuting(true);
    try {
      const res = await apiFetch('/api/v1/git/stage', {
        method: 'POST',
        body: { workspaceId, files, stageAll },
      });
      if (res.ok) {
        fetchStatus();
      } else {
        showToast((res.data as any)?.message || res.error || 'Failed to stage changes.', 'error');
      }
    } finally {
      setIsExecuting(false);
    }
  };

  const handleUnstage = async (files?: string[], unstageAll = false) => {
    setIsExecuting(true);
    try {
      const res = await apiFetch('/api/v1/git/unstage', {
        method: 'POST',
        body: { workspaceId, files, unstageAll },
      });
      if (res.ok) {
        fetchStatus();
      } else {
        showToast((res.data as any)?.message || res.error || 'Failed to unstage changes.', 'error');
      }
    } finally {
      setIsExecuting(false);
    }
  };

  const handleCommit = async () => {
    if (!commitMessage.trim()) return;
    setIsExecuting(true);
    try {
      const res = await apiFetch('/api/v1/git/commit', {
        method: 'POST',
        body: { workspaceId, message: commitMessage.trim() },
      });
      if (res.ok) {
        showToast('Commit created successfully.', 'success');
        setCommitMessage('');
        refreshAll();
      } else {
        showToast((res.data as any)?.message || res.error || 'Failed to commit changes.', 'error');
      }
    } finally {
      setIsExecuting(false);
    }
  };

  const handleStash = async (action: 'save' | 'pop') => {
    setIsExecuting(true);
    try {
      const res = await apiFetch('/api/v1/git/stash', {
        method: 'POST',
        body: { workspaceId, action },
      });
      if (res.ok) {
        showToast(action === 'save' ? 'Changes stashed.' : 'Stash popped.', 'success');
        refreshAll();
      } else {
        showToast((res.data as any)?.message || res.error || 'Failed to perform stash action.', 'error');
      }
    } finally {
      setIsExecuting(false);
    }
  };

  // View Diff for working tree file or commit file
  const handleViewDiff = async (filePath: string) => {
    try {
      const res = await apiFetch<FileChangeDto>(
        `/api/v1/git/commit-diff?workspaceId=${workspaceId}&hash=${selectedRowId}&filePath=${encodeURIComponent(filePath)}`
      );
      if (res.ok && res.data) {
        setDiffFileChange(res.data);
      } else {
        showToast((res.data as any)?.message || res.error || 'Could not load diff for file.', 'error');
      }
    } catch {
      showToast('Error loading diff.', 'error');
    }
  };

  if (isLoading && !status) {
    return (
      <div
        className="git-workbench-loading glass"
        style={{
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          justifyContent: 'center',
          height: '100%',
          gap: '12px',
          color: 'var(--text-muted)',
        }}
      >
        <Spinner size={24} isSpinning={true} />
        <span style={{ fontSize: '0.9rem' }}>Loading Git Workbench...</span>
      </div>
    );
  }

  if (status && !status.isGitInstalled) {
    return (
      <div className="git-workbench-empty glass" style={{ padding: '32px', textAlign: 'center', color: 'var(--text-muted)' }}>
        <p style={{ fontSize: '1.4rem', marginBottom: '8px' }}>⚠️ Git Not Detected</p>
        <p style={{ fontSize: '0.9rem' }}>
          Git is not installed on this server host machine or is not available on PATH.
        </p>
      </div>
    );
  }

  if (status && !status.isRepository) {
    return (
      <div className="git-workbench-empty glass" style={{ padding: '32px', textAlign: 'center', color: 'var(--text-muted)' }}>
        <p style={{ fontSize: '1.4rem', marginBottom: '8px' }}>📁 Not a Git Repository</p>
        <p style={{ fontSize: '0.9rem' }}>
          This workspace directory has not been initialized as a Git repository.
        </p>
      </div>
    );
  }

  return (
    <div
      className="git-workbench glass"
      style={{
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        width: '100%',
        overflow: 'hidden',
        position: 'relative',
      }}
      id="gitWorkbench"
    >
      {/* Top Pane: Commit Log Table */}
      <div style={{ height: `${splitRatio}%`, overflow: 'hidden' }}>
        <GitCommitLogTable
          commits={history}
          status={status}
          selectedRowId={selectedRowId}
          onSelectRow={(id) => setSelectedRowId(id)}
          branches={branches}
          onCheckoutBranch={handleCheckoutBranch}
          onCreateBranch={handleCreateBranch}
          onFetch={handleFetch}
          onPull={handlePull}
          onPush={handlePush}
          onRefresh={refreshAll}
          isExecuting={isExecuting}
          isLoading={isLoading}
        />
      </div>

      {/* Pane Splitter / Resizer */}
      <div
        className="git-workbench-splitter"
        role="separator"
        tabIndex={0}
        aria-orientation="horizontal"
        aria-valuenow={Math.round(splitRatio * 100)}
        aria-valuemin={20}
        aria-valuemax={80}
        aria-label="Resize panels"
        onKeyDown={(e) => {
          if (e.key === 'ArrowUp') {
            e.preventDefault();
            setSplitRatio((prev) => Math.max(0.2, prev - 0.05));
          } else if (e.key === 'ArrowDown') {
            e.preventDefault();
            setSplitRatio((prev) => Math.min(0.8, prev + 0.05));
          }
        }}
        style={{
          height: '6px',
          background: 'var(--border-color)',
          cursor: 'row-resize',
          position: 'relative',
          zIndex: 3,
          transition: 'background 0.2s',
        }}
        title="Drag or use arrow keys to resize panes"
        onMouseDown={(e) => {
          const startY = e.clientY;
          const startRatio = splitRatio;
          const container = e.currentTarget.parentElement;
          if (!container) return;
          const containerHeight = container.clientHeight;

          const onMouseMove = (moveEvent: MouseEvent) => {
            const deltaY = moveEvent.clientY - startY;
            const newRatio = Math.min(80, Math.max(20, startRatio + (deltaY / containerHeight) * 100));
            setSplitRatio(newRatio);
          };

          const onMouseUp = () => {
            window.removeEventListener('mousemove', onMouseMove);
            window.removeEventListener('mouseup', onMouseUp);
          };

          window.addEventListener('mousemove', onMouseMove);
          window.addEventListener('mouseup', onMouseUp);
        }}
      />

      {/* Bottom Pane: Context Inspector */}
      <div style={{ height: `${100 - splitRatio}%`, overflow: 'hidden' }}>
        {selectedRowId === 'working-tree' ? (
          <GitWorkingTreeInspector
            status={status}
            commitMessage={commitMessage}
            onCommitMessageChange={setCommitMessage}
            onCommit={handleCommit}
            onStage={handleStage}
            onUnstage={handleUnstage}
            onViewDiff={handleViewDiff}
            onFileSelect={onFileSelect}
            onStash={handleStash}
            isExecuting={isExecuting}
          />
        ) : (
          <GitCommitDetailInspector
            workspaceId={workspaceId}
            commitHash={selectedRowId}
            onViewDiff={handleViewDiff}
            onFileSelect={onFileSelect}
          />
        )}
      </div>

      {/* Read-Only Diff Modal */}
      {diffFileChange && (
        <DiffViewerModal
          workspaceId={workspaceId}
          readOnly={true}
          customChanges={[diffFileChange]}
          initialFileChangeId={diffFileChange.id}
          onClose={() => setDiffFileChange(null)}
        />
      )}
    </div>
  );
};

// Simple Modal for Branch creation
const CreateBranchModal: React.FC<{ onSubmit: (name: string) => void; onCancel: () => void }> = ({
  onSubmit,
  onCancel,
}) => {
  const [name, setName] = useState('');
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        if (name.trim()) onSubmit(name.trim());
      }}
      style={{ minWidth: '300px' }}
    >
      <div className="form-group" style={{ marginBottom: '14px' }}>
        <label htmlFor="workbenchBranchNameInput" className="form-label">Branch Name</label>
        <input
          id="workbenchBranchNameInput"
          type="text"
          className="form-control"
          placeholder="e.g. feature/my-new-feature"
          value={name}
          onChange={(e) => setName(e.target.value)}
          autoFocus
          required
        />
      </div>
      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
        <button type="button" className="btn btn-secondary" onClick={onCancel}>
          Cancel
        </button>
        <button type="submit" className="btn btn-primary" disabled={!name.trim()}>
          Create & Checkout
        </button>
      </div>
    </form>
  );
};
