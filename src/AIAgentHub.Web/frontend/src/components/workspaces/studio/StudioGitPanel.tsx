import React, { useState, useEffect, useCallback } from 'react';
import { apiFetch } from '../../../services/apiClient';
import { GitStatusDto, GitBranchDto, GitCommitDto } from '../../../types/git';
import { useToast } from '../../../context/ToastContext';
import { useModal } from '../../../context/ModalContext';
import { Spinner } from '../../common/Spinner';
import { GitBranchIcon } from '../../common/Icons';

interface StudioGitPanelProps {
  workspaceId: string;
  isOpen: boolean;
  mobileTab?: 'chat' | 'sidebar' | 'git';
  onClose: () => void;
  onFileSelect?: (path: string) => void;
}

function useGitOperations({
  workspaceId,
  refreshAll,
  fetchStatus,
  commitMessage,
  setCommitMessage,
}: {
  workspaceId: string;
  refreshAll: () => void;
  fetchStatus: () => void;
  commitMessage: string;
  setCommitMessage: (msg: string) => void;
}) {
  const { showToast } = useToast();
  const { showModal, hideModal } = useModal();
  const [isExecuting, setIsExecuting] = useState<boolean>(false);

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

  return {
    isExecuting,
    handleCheckoutBranch,
    handleCreateBranch,
    handleStage,
    handleUnstage,
    handleCommit,
    handlePush,
    handlePull,
    handleFetch,
    handleStash,
  };
}

export const GitNotReadyNotice: React.FC<{ status: GitStatusDto }> = ({ status }) => {
  if (!status.isGitInstalled) {
    return (
      <div style={{ padding: '16px', color: 'var(--text-muted)', textAlign: 'center' }}>
        <p style={{ fontSize: '1.2rem', marginBottom: '8px' }}>⚠️ Git Not Detected</p>
        <p style={{ fontSize: '0.85rem' }}>
          Git is not installed on this server host machine or is not available on PATH.
        </p>
      </div>
    );
  }
  if (!status.isRepository) {
    return (
      <div style={{ padding: '16px', color: 'var(--text-muted)', textAlign: 'center' }}>
        <p style={{ fontSize: '1.1rem', marginBottom: '8px' }}>📁 Not a Git Repository</p>
        <p style={{ fontSize: '0.85rem' }}>
          This workspace directory has not been initialized as a Git repository.
        </p>
      </div>
    );
  }
  return null;
};

export const StudioGitPanel: React.FC<StudioGitPanelProps> = ({
  workspaceId,
  isOpen,
  mobileTab,
  onClose,
  onFileSelect,
}) => {
  const [status, setStatus] = useState<GitStatusDto | null>(null);
  const [branches, setBranches] = useState<GitBranchDto[]>([]);
  const [history, setHistory] = useState<GitCommitDto[]>([]);
  const [commitMessage, setCommitMessage] = useState<string>('');
  const [isLoadingStatus, setIsLoadingStatus] = useState<boolean>(false);
  const [activeTab, setActiveTab] = useState<'changes' | 'history'>('changes');

  const fetchStatus = useCallback(async () => {
    setIsLoadingStatus(true);
    try {
      const res = await apiFetch<GitStatusDto>(`/api/v1/git/status?workspaceId=${workspaceId}`);
      if (res.ok && res.data) {
        setStatus(res.data);
      }
    } catch {
      // Ignore
    } finally {
      setIsLoadingStatus(false);
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
      const res = await apiFetch<GitCommitDto[]>(`/api/v1/git/history?workspaceId=${workspaceId}&maxCount=30`);
      if (res.ok && res.data) {
        setHistory(res.data);
      }
    } catch {
      // Ignore
    }
  }, [workspaceId]);

  const refreshAll = useCallback(() => {
    fetchStatus();
    fetchBranches();
    fetchHistory();
  }, [fetchStatus, fetchBranches, fetchHistory]);

  const {
    isExecuting,
    handleCheckoutBranch,
    handleCreateBranch,
    handleStage,
    handleUnstage,
    handleCommit,
    handlePush,
    handlePull,
    handleFetch,
    handleStash,
  } = useGitOperations({
    workspaceId,
    refreshAll,
    fetchStatus,
    commitMessage,
    setCommitMessage,
  });

  useEffect(() => {
    if (isOpen) {
      refreshAll();
    }
  }, [isOpen, refreshAll]);

  if (!isOpen) return null;

  return (
    <div
      className={`studio-git-drawer glass ${mobileTab && mobileTab !== 'git' ? 'mobile-hidden' : ''}`}
    >
      {/* Header */}
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          padding: '10px 14px',
          borderBottom: '1px solid var(--border-color)',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          <strong style={{ fontSize: '0.95rem', color: 'var(--text-heading)', display: 'flex', alignItems: 'center', gap: '6px' }}>
            <GitBranchIcon size={16} style={{ color: '#4ade80' }} /> Git Control
          </strong>
          {isLoadingStatus && <Spinner size={14} isSpinning={true} />}
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
          <button
            type="button"
            className="btn-icon-plain"
            onClick={refreshAll}
            disabled={isExecuting || isLoadingStatus}
            title="Refresh Git status"
            id="gitRefreshBtn"
          >
            🔄
          </button>
          <button
            type="button"
            className="btn-icon-plain"
            onClick={onClose}
            title="Close Git drawer"
            id="gitCloseDrawerBtn"
            style={{ fontSize: '1.1rem' }}
          >
            ✕
          </button>
        </div>
      </div>

      {/* Content */}
      <div className="studio-git-drawer-content">
        {status && (!status.isGitInstalled || !status.isRepository) ? (
          <GitNotReadyNotice status={status} />
        ) : (
          <>
            {/* Branch Switcher & Sync Row */}
            <div
              style={{
                background: 'var(--bg-subtle)',
                padding: '10px',
                borderRadius: '6px',
                border: '1px solid var(--border-color)',
                marginBottom: '12px',
              }}
            >
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
                <span style={{ fontSize: '0.78rem', color: 'var(--text-muted)' }}>Branch:</span>
                <button
                  type="button"
                  className="btn btn-secondary compact-btn"
                  onClick={handleCreateBranch}
                  disabled={isExecuting}
                  style={{ fontSize: '0.75rem', padding: '2px 8px' }}
                  id="gitNewBranchBtn"
                >
                  + New Branch
                </button>
              </div>

              <select
                className="form-select"
                value={status?.currentBranch || ''}
                onChange={(e) => handleCheckoutBranch(e.target.value)}
                disabled={isExecuting}
                style={{ width: '100%', maxWidth: 'none', fontSize: '0.82rem' }}
                id="gitBranchSelect"
              >
                {status?.currentBranch && !branches.some((b) => b.name === status.currentBranch) && (
                  <option key={status.currentBranch} value={status.currentBranch}>
                    {status.currentBranch} (current)
                  </option>
                )}
                {branches.map((b) => (
                  <option key={b.name} value={b.name}>
                    {b.name} {b.isCurrent ? '(current)' : ''}
                  </option>
                ))}
              </select>

              {/* Ahead / Behind Sync Info */}
              <div
                style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  marginTop: '8px',
                  fontSize: '0.78rem',
                  color: 'var(--text-muted)',
                }}
              >
                <span>
                  {status && (status.ahead > 0 || status.behind > 0) ? (
                    <>
                      {status.ahead > 0 && <span style={{ color: '#4ade80', marginRight: '6px' }}>↑ {status.ahead} ahead</span>}
                      {status.behind > 0 && <span style={{ color: '#f87171' }}>↓ {status.behind} behind</span>}
                    </>
                  ) : (
                    <span>Synced with remote</span>
                  )}
                </span>

                <div style={{ display: 'flex', gap: '4px' }}>
                  <button
                    type="button"
                    className="btn btn-secondary compact-btn"
                    onClick={handleFetch}
                    disabled={isExecuting}
                    style={{ fontSize: '0.72rem', padding: '1px 6px' }}
                    title="Fetch from remote"
                    id="gitFetchBtn"
                  >
                    Fetch
                  </button>
                  <button
                    type="button"
                    className="btn btn-secondary compact-btn"
                    onClick={handlePull}
                    disabled={isExecuting}
                    style={{ fontSize: '0.72rem', padding: '1px 6px' }}
                    title="Pull from remote"
                    id="gitPullBtn"
                  >
                    ⬇️ Pull
                  </button>
                  <button
                    type="button"
                    className="btn btn-secondary compact-btn"
                    onClick={handlePush}
                    disabled={isExecuting || !status?.ahead}
                    style={{ fontSize: '0.72rem', padding: '1px 6px' }}
                    title="Push commits to remote"
                    id="gitPushBtn"
                  >
                    ⬆️ Push
                  </button>
                </div>
              </div>
            </div>

            {/* View Sub-Tabs (Changes vs History) */}
            <div style={{ display: 'flex', gap: '4px', marginBottom: '10px' }}>
              <button
                type="button"
                className={`btn compact-btn ${activeTab === 'changes' ? 'btn-primary' : 'btn-secondary'}`}
                onClick={() => setActiveTab('changes')}
                style={{ flex: 1, fontSize: '0.8rem', padding: '4px 8px' }}
              >
                Changes ({(status?.staged.length || 0) + (status?.modified.length || 0) + (status?.untracked.length || 0)})
              </button>
              <button
                type="button"
                className={`btn compact-btn ${activeTab === 'history' ? 'btn-primary' : 'btn-secondary'}`}
                onClick={() => {
                  setActiveTab('history');
                  fetchHistory();
                }}
                style={{ flex: 1, fontSize: '0.8rem', padding: '4px 8px' }}
              >
                History ({history.length})
              </button>
            </div>

            {activeTab === 'changes' ? (
              <>
                {/* Stash buttons */}
                <div style={{ display: 'flex', gap: '6px', marginBottom: '10px' }}>
                  <button
                    type="button"
                    className="btn btn-secondary compact-btn"
                    onClick={() => handleStash('save')}
                    disabled={isExecuting || ((status?.staged.length || 0) + (status?.modified.length || 0) === 0)}
                    style={{ flex: 1, fontSize: '0.75rem', padding: '3px 8px' }}
                    title="Stash uncommitted changes"
                    id="gitStashBtn"
                  >
                    📦 Stash
                  </button>
                  <button
                    type="button"
                    className="btn btn-secondary compact-btn"
                    onClick={() => handleStash('pop')}
                    disabled={isExecuting}
                    style={{ flex: 1, fontSize: '0.75rem', padding: '3px 8px' }}
                    title="Pop most recent stash"
                    id="gitPopStashBtn"
                  >
                    📤 Pop Stash
                  </button>
                </div>

                {/* Staged Changes */}
                <div style={{ marginBottom: '12px' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '4px' }}>
                    <strong style={{ fontSize: '0.8rem', color: '#4ade80' }}>
                      Staged Changes ({status?.staged.length || 0})
                    </strong>
                    {(status?.staged.length || 0) > 0 && (
                      <button
                        type="button"
                        className="btn-icon-plain"
                        onClick={() => handleUnstage(undefined, true)}
                        disabled={isExecuting}
                        title="Unstage all files"
                        style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}
                      >
                        Unstage All
                      </button>
                    )}
                  </div>
                  {status?.staged.length === 0 ? (
                    <div style={{ fontSize: '0.76rem', color: 'var(--text-muted)', padding: '4px 0' }}>
                      No staged changes.
                    </div>
                  ) : (
                    <div className="git-file-list-group">
                      {status?.staged.map((f) => (
                        <div
                          key={f.path}
                          style={{
                            display: 'flex',
                            justifyContent: 'space-between',
                            alignItems: 'center',
                            padding: '3px 6px',
                            borderRadius: '4px',
                            fontSize: '0.8rem',
                            background: 'rgba(34, 197, 94, 0.08)',
                            marginBottom: '2px',
                          }}
                        >
                          <button
                            type="button"
                            onClick={() => onFileSelect?.(f.path)}
                            style={{
                              background: 'none',
                              border: 'none',
                              padding: 0,
                              color: 'inherit',
                              font: 'inherit',
                              cursor: 'pointer',
                              overflow: 'hidden',
                              textOverflow: 'ellipsis',
                              whiteSpace: 'nowrap',
                              textAlign: 'left',
                            }}
                            title={`Click to view: ${f.path}`}
                          >
                            📄 {f.path}
                          </button>
                          <button
                            type="button"
                            className="btn-icon-plain"
                            onClick={() => handleUnstage([f.path])}
                            disabled={isExecuting}
                            title="Unstage this file"
                            style={{ fontSize: '0.8rem', color: '#f87171' }}
                          >
                            −
                          </button>
                        </div>
                      ))}
                    </div>
                  )}
                </div>

                {/* Modified Changes */}
                <div style={{ marginBottom: '12px' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '4px' }}>
                    <strong style={{ fontSize: '0.8rem', color: '#f59e0b' }}>
                      Modified Changes ({status?.modified.length || 0})
                    </strong>
                    {(status?.modified.length || 0) > 0 && (
                      <button
                        type="button"
                        className="btn-icon-plain"
                        onClick={() => handleStage(undefined, true)}
                        disabled={isExecuting}
                        title="Stage all modified files"
                        style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}
                      >
                        Stage All
                      </button>
                    )}
                  </div>
                  {status?.modified.length === 0 ? (
                    <div style={{ fontSize: '0.76rem', color: 'var(--text-muted)', padding: '4px 0' }}>
                      No modified changes.
                    </div>
                  ) : (
                    <div className="git-file-list-group">
                      {status?.modified.map((f) => (
                        <div
                          key={f.path}
                          style={{
                            display: 'flex',
                            justifyContent: 'space-between',
                            alignItems: 'center',
                            padding: '3px 6px',
                            borderRadius: '4px',
                            fontSize: '0.8rem',
                            background: 'rgba(245, 158, 11, 0.08)',
                            marginBottom: '2px',
                          }}
                        >
                          <button
                            type="button"
                            onClick={() => onFileSelect?.(f.path)}
                            style={{
                              background: 'none',
                              border: 'none',
                              padding: 0,
                              color: 'inherit',
                              font: 'inherit',
                              cursor: 'pointer',
                              overflow: 'hidden',
                              textOverflow: 'ellipsis',
                              whiteSpace: 'nowrap',
                              textAlign: 'left',
                            }}
                            title={`Click to view: ${f.path}`}
                          >
                            ✏️ {f.path}
                          </button>
                          <button
                            type="button"
                            className="btn-icon-plain"
                            onClick={() => handleStage([f.path])}
                            disabled={isExecuting}
                            title="Stage this file"
                            style={{ fontSize: '0.8rem', color: '#4ade80' }}
                          >
                            +
                          </button>
                        </div>
                      ))}
                    </div>
                  )}
                </div>

                {/* Untracked Files */}
                {(status?.untracked.length || 0) > 0 && (
                  <div style={{ marginBottom: '12px' }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '4px' }}>
                      <strong style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>
                        Untracked ({status?.untracked.length})
                      </strong>
                    </div>
                    <div className="git-file-list-group">
                      {status?.untracked.map((path) => (
                        <div
                          key={path}
                          style={{
                            display: 'flex',
                            justifyContent: 'space-between',
                            alignItems: 'center',
                            padding: '3px 6px',
                            borderRadius: '4px',
                            fontSize: '0.8rem',
                            background: 'rgba(255, 255, 255, 0.04)',
                            marginBottom: '2px',
                          }}
                        >
                          <button
                            type="button"
                            onClick={() => onFileSelect?.(path)}
                            style={{
                              background: 'none',
                              border: 'none',
                              padding: 0,
                              color: 'inherit',
                              font: 'inherit',
                              cursor: 'pointer',
                              overflow: 'hidden',
                              textOverflow: 'ellipsis',
                              whiteSpace: 'nowrap',
                              textAlign: 'left',
                            }}
                            title={`Click to view: ${path}`}
                          >
                            ❓ {path}
                          </button>
                          <button
                            type="button"
                            className="btn-icon-plain"
                            onClick={() => handleStage([path])}
                            disabled={isExecuting}
                            title="Stage this untracked file"
                            style={{ fontSize: '0.8rem', color: '#4ade80' }}
                          >
                            +
                          </button>
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                {/* Commit Box */}
                <div
                  style={{
                    background: 'var(--bg-subtle)',
                    padding: '10px',
                    borderRadius: '6px',
                    border: '1px solid var(--border-color)',
                    marginTop: '12px',
                  }}
                >
                  <label
                    htmlFor="gitCommitMessageInput"
                    className="form-label"
                    style={{ fontSize: '0.78rem', marginBottom: '4px' }}
                  >
                    Commit Message
                  </label>
                  <textarea
                    className="form-textarea"
                    rows={2}
                    placeholder="Describe your changes..."
                    value={commitMessage}
                    onChange={(e) => setCommitMessage(e.target.value)}
                    disabled={isExecuting}
                    style={{ fontSize: '0.8rem', resize: 'vertical', marginBottom: '8px' }}
                    id="gitCommitMessageInput"
                  />
                  <button
                    type="button"
                    className="btn btn-primary compact-btn"
                    onClick={handleCommit}
                    disabled={isExecuting || !commitMessage.trim() || (status?.staged.length || 0) === 0}
                    style={{ width: '100%', fontSize: '0.8rem' }}
                    id="gitCommitBtn"
                  >
                    {isExecuting ? 'Committing...' : `Commit (${status?.staged.length || 0} staged)`}
                  </button>
                </div>
              </>
            ) : (
              /* Commit History */
              <div className="git-history-list">
                {history.length === 0 ? (
                  <div style={{ padding: '16px', color: 'var(--text-muted)', fontSize: '0.82rem' }}>
                    No commit history found.
                  </div>
                ) : (
                  history.map((c) => (
                    <div
                      key={c.hash}
                      style={{
                        padding: '8px 10px',
                        borderRadius: '6px',
                        border: '1px solid var(--border-color)',
                        background: 'var(--bg-subtle)',
                        marginBottom: '8px',
                      }}
                    >
                      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '4px' }}>
                        <code style={{ fontSize: '0.75rem', color: 'var(--accent-primary)' }}>
                          {c.shortHash}
                        </code>
                        <span style={{ fontSize: '0.72rem', color: 'var(--text-muted)' }}>
                          {new Date(c.date).toLocaleDateString()}
                        </span>
                      </div>
                      <div style={{ fontSize: '0.82rem', fontWeight: 500, color: 'var(--text-heading)', marginBottom: '4px' }}>
                        {c.message}
                      </div>
                      <div style={{ fontSize: '0.72rem', color: 'var(--text-muted)' }}>
                        by {c.author}
                      </div>
                    </div>
                  ))
                )}
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );
};

// Modal for Branch creation
export const CreateBranchModal: React.FC<{ onSubmit: (name: string) => void; onCancel: () => void }> = ({
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
        <label htmlFor="gitModalBranchNameInput" className="form-label">Branch Name</label>
        <input
          id="gitModalBranchNameInput"
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
