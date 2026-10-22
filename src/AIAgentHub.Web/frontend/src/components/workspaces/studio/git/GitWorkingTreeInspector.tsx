import React from 'react';
import { GitStatusDto } from '../../../../types/git';

export interface GitWorkingTreeInspectorProps {
  status: GitStatusDto | null;
  commitMessage: string;
  onCommitMessageChange: (msg: string) => void;
  onCommit: () => void;
  onStage: (files?: string[], stageAll?: boolean) => void;
  onUnstage: (files?: string[], unstageAll?: boolean) => void;
  onViewDiff: (filePath: string) => void;
  onFileSelect?: (filePath: string) => void;
  onStash: (action: 'save' | 'pop') => void;
  isExecuting?: boolean;
}

export const GitWorkingTreeInspector: React.FC<GitWorkingTreeInspectorProps> = ({
  status,
  commitMessage,
  onCommitMessageChange,
  onCommit,
  onStage,
  onUnstage,
  onViewDiff,
  onFileSelect,
  onStash,
  isExecuting = false,
}) => {
  const staged = status?.staged || [];
  const modified = status?.modified || [];
  const untracked = status?.untracked || [];
  const totalCount = staged.length + modified.length + untracked.length;

  return (
    <div className="git-working-tree-inspector">
      {/* Left Pane: Changed Files (Staged, Modified, Untracked) */}
      <div className="git-inspector-files-pane">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '12px' }}>
          <strong style={{ fontSize: '0.88rem', color: 'var(--text-heading)' }}>
            Uncommitted Changes ({totalCount})
          </strong>
          {modified.length > 0 && (
            <button
              type="button"
              className="btn btn-secondary compact-btn"
              onClick={() => onStage(undefined, true)}
              disabled={isExecuting}
              style={{ fontSize: '0.75rem', padding: '2px 8px' }}
              id="stageAllBtn"
            >
              + Stage All
            </button>
          )}
        </div>

        {totalCount === 0 ? (
          <div style={{ padding: '28px 16px', textAlign: 'center', color: 'var(--text-muted)' }}>
            <p style={{ fontSize: '1.2rem', marginBottom: '6px' }}>✨</p>
            <p style={{ fontSize: '0.85rem', fontWeight: 500 }}>Working directory is clean.</p>
            <p style={{ fontSize: '0.78rem' }}>No uncommitted changes detected in this workspace.</p>
          </div>
        ) : (
          <>
            {/* 1. Staged Changes */}
            <div style={{ marginBottom: '14px' }}>
              <div
                style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  marginBottom: '6px',
                  borderBottom: '1px solid var(--border-color)',
                  paddingBottom: '4px',
                }}
              >
                <span style={{ fontSize: '0.8rem', fontWeight: 600, color: '#4ade80' }}>
                  Staged Changes ({staged.length})
                </span>
                {staged.length > 0 && (
                  <button
                    type="button"
                    className="btn-icon-plain"
                    onClick={() => onUnstage(undefined, true)}
                    disabled={isExecuting}
                    style={{ fontSize: '0.74rem', color: 'var(--text-muted)' }}
                    title="Unstage all staged files"
                  >
                    Unstage All
                  </button>
                )}
              </div>

              {staged.length === 0 ? (
                <div style={{ fontSize: '0.76rem', color: 'var(--text-muted)', padding: '2px 0' }}>
                  No files staged for commit.
                </div>
              ) : (
                <div className="git-file-list-group">
                  {staged.map((f) => (
                    <div
                      key={`staged-${f.path}`}
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'space-between',
                        padding: '4px 8px',
                        borderRadius: '4px',
                        fontSize: '0.8rem',
                        background: 'rgba(34, 197, 94, 0.08)',
                        marginBottom: '3px',
                      }}
                    >
                      <div
                        style={{
                          display: 'flex',
                          alignItems: 'center',
                          gap: '6px',
                          overflow: 'hidden',
                          textOverflow: 'ellipsis',
                          whiteSpace: 'nowrap',
                          flex: 1,
                          minWidth: 0,
                        }}
                      >
                        <span
                          className="badge"
                          style={{
                            fontSize: '0.68rem',
                            padding: '1px 4px',
                            background: '#22c55e',
                            color: '#000',
                            fontWeight: 700,
                            flexShrink: 0,
                          }}
                        >
                          {f.status.toUpperCase()}
                        </span>
                        <button
                          type="button"
                          onClick={() => onFileSelect?.(f.path)}
                          style={{
                            background: 'none',
                            border: 'none',
                            padding: 0,
                            cursor: 'pointer',
                            overflow: 'hidden',
                            textOverflow: 'ellipsis',
                            minWidth: 0,
                            textAlign: 'left',
                            color: 'var(--text-main)',
                            fontSize: 'inherit',
                          }}
                          title={`Click to open in editor: ${f.path}`}
                        >
                          {f.path}
                        </button>
                      </div>

                      <div style={{ display: 'flex', alignItems: 'center', gap: '6px', flexShrink: 0 }}>
                        <button
                          type="button"
                          className="btn btn-secondary compact-btn"
                          onClick={() => onViewDiff(f.path)}
                          disabled={isExecuting}
                          style={{ fontSize: '0.72rem', padding: '1px 6px' }}
                          title="View diff vs HEAD"
                        >
                          Diff
                        </button>
                        <button
                          type="button"
                          className="btn-icon-plain"
                          onClick={() => onUnstage([f.path])}
                          disabled={isExecuting}
                          style={{ fontSize: '0.85rem', color: '#f87171', padding: '0 4px' }}
                          title="Unstage this file"
                        >
                          −
                        </button>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>

            {/* 2. Modified Changes */}
            <div style={{ marginBottom: '14px' }}>
              <div
                style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  marginBottom: '6px',
                  borderBottom: '1px solid var(--border-color)',
                  paddingBottom: '4px',
                }}
              >
                <span style={{ fontSize: '0.8rem', fontWeight: 600, color: '#f59e0b' }}>
                  Modified / Changes ({modified.length})
                </span>
                {modified.length > 0 && (
                  <button
                    type="button"
                    className="btn-icon-plain"
                    onClick={() => onStage(undefined, true)}
                    disabled={isExecuting}
                    style={{ fontSize: '0.74rem', color: '#4ade80' }}
                    title="Stage all modified files"
                  >
                    + Stage All
                  </button>
                )}
              </div>

              {modified.length === 0 ? (
                <div style={{ fontSize: '0.76rem', color: 'var(--text-muted)', padding: '2px 0' }}>
                  No unstaged modifications.
                </div>
              ) : (
                <div className="git-file-list-group">
                  {modified.map((f) => (
                    <div
                      key={`mod-${f.path}`}
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'space-between',
                        padding: '4px 8px',
                        borderRadius: '4px',
                        fontSize: '0.8rem',
                        background: 'rgba(245, 158, 11, 0.08)',
                        marginBottom: '3px',
                      }}
                    >
                      <div
                        style={{
                          display: 'flex',
                          alignItems: 'center',
                          gap: '6px',
                          overflow: 'hidden',
                          textOverflow: 'ellipsis',
                          whiteSpace: 'nowrap',
                          flex: 1,
                          minWidth: 0,
                        }}
                      >
                        <span
                          className="badge"
                          style={{
                            fontSize: '0.68rem',
                            padding: '1px 4px',
                            background: '#f59e0b',
                            color: '#000',
                            fontWeight: 700,
                            flexShrink: 0,
                          }}
                        >
                          {f.status.toUpperCase()}
                        </span>
                        <button
                          type="button"
                          onClick={() => onFileSelect?.(f.path)}
                          style={{
                            background: 'none',
                            border: 'none',
                            padding: 0,
                            cursor: 'pointer',
                            overflow: 'hidden',
                            textOverflow: 'ellipsis',
                            minWidth: 0,
                            textAlign: 'left',
                            color: 'var(--text-main)',
                            fontSize: 'inherit',
                          }}
                          title={`Click to open in editor: ${f.path}`}
                        >
                          {f.path}
                        </button>
                      </div>

                      <div style={{ display: 'flex', alignItems: 'center', gap: '6px', flexShrink: 0 }}>
                        <button
                          type="button"
                          className="btn btn-secondary compact-btn"
                          onClick={() => onViewDiff(f.path)}
                          disabled={isExecuting}
                          style={{ fontSize: '0.72rem', padding: '1px 6px' }}
                          title="View diff vs HEAD"
                        >
                          Diff
                        </button>
                        <button
                          type="button"
                          className="btn-icon-plain"
                          onClick={() => onStage([f.path])}
                          disabled={isExecuting}
                          style={{ fontSize: '0.85rem', color: '#4ade80', padding: '0 4px' }}
                          title="Stage this file"
                        >
                          +
                        </button>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>

            {/* 3. Untracked Files */}
            {untracked.length > 0 && (
              <div style={{ marginBottom: '14px' }}>
                <div
                  style={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                    marginBottom: '6px',
                    borderBottom: '1px solid var(--border-color)',
                    paddingBottom: '4px',
                  }}
                >
                  <span style={{ fontSize: '0.8rem', fontWeight: 600, color: 'var(--text-muted)' }}>
                    Untracked Files ({untracked.length})
                  </span>
                  <button
                    type="button"
                    className="btn-icon-plain"
                    onClick={() => onStage(untracked)}
                    disabled={isExecuting}
                    style={{ fontSize: '0.74rem', color: '#4ade80' }}
                    title="Stage all untracked files"
                  >
                    + Stage All
                  </button>
                </div>

                <div className="git-file-list-group">
                  {untracked.map((path) => (
                    <div
                      key={`untracked-${path}`}
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'space-between',
                        padding: '4px 8px',
                        borderRadius: '4px',
                        fontSize: '0.8rem',
                        background: 'rgba(255, 255, 255, 0.04)',
                        marginBottom: '3px',
                      }}
                    >
                      <div
                        style={{
                          display: 'flex',
                          alignItems: 'center',
                          gap: '6px',
                          overflow: 'hidden',
                          textOverflow: 'ellipsis',
                          whiteSpace: 'nowrap',
                          flex: 1,
                          minWidth: 0,
                        }}
                      >
                        <span
                          className="badge"
                          style={{
                            fontSize: '0.68rem',
                            padding: '1px 4px',
                            background: 'rgba(255, 255, 255, 0.2)',
                            color: 'var(--text-heading)',
                            fontWeight: 700,
                            flexShrink: 0,
                          }}
                        >
                          ?
                        </span>
                        <button
                          type="button"
                          onClick={() => onFileSelect?.(path)}
                          style={{
                            background: 'none',
                            border: 'none',
                            padding: 0,
                            cursor: 'pointer',
                            overflow: 'hidden',
                            textOverflow: 'ellipsis',
                            minWidth: 0,
                            textAlign: 'left',
                            color: 'var(--text-main)',
                            fontSize: 'inherit',
                          }}
                          title={`Click to open in editor: ${path}`}
                        >
                          {path}
                        </button>
                      </div>

                      <div style={{ display: 'flex', alignItems: 'center', gap: '6px', flexShrink: 0 }}>
                        <button
                          type="button"
                          className="btn-icon-plain"
                          onClick={() => onStage([path])}
                          disabled={isExecuting}
                          style={{ fontSize: '0.85rem', color: '#4ade80', padding: '0 4px' }}
                          title="Stage this file"
                        >
                          +
                        </button>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </>
        )}
      </div>

      {/* Right Pane: Commit & Stash Controls */}
      <div className="git-inspector-commit-pane">
        <strong style={{ fontSize: '0.85rem', color: 'var(--text-heading)', marginBottom: '8px' }}>
          Commit Message
        </strong>
        <textarea
          className="form-textarea"
          rows={4}
          placeholder="Enter commit message (e.g. feat: add new feature)..."
          value={commitMessage}
          onChange={(e) => onCommitMessageChange(e.target.value)}
          disabled={isExecuting}
          style={{ fontSize: '0.8rem', resize: 'vertical', marginBottom: '10px' }}
          id="workbenchCommitMessageInput"
        />

        <button
          type="button"
          className="btn btn-primary"
          onClick={onCommit}
          disabled={isExecuting || !commitMessage.trim() || staged.length === 0}
          style={{ width: '100%', marginBottom: '12px', fontSize: '0.82rem', padding: '6px 12px' }}
          id="workbenchCommitSubmitBtn"
        >
          {isExecuting ? 'Committing...' : `Commit (${staged.length} staged)`}
        </button>

        {staged.length === 0 && modified.length > 0 && (
          <button
            type="button"
            className="btn btn-secondary compact-btn"
            onClick={() => {
              onStage(undefined, true);
            }}
            disabled={isExecuting}
            style={{ width: '100%', marginBottom: '12px', fontSize: '0.78rem' }}
          >
            Stage All Modified Files First
          </button>
        )}

        {/* Stash Section */}
        <div style={{ marginTop: 'auto', paddingTop: '12px', borderTop: '1px solid var(--border-color)' }}>
          <span style={{ fontSize: '0.78rem', color: 'var(--text-muted)', display: 'block', marginBottom: '8px' }}>
            Working Tree Stash
          </span>
          <div style={{ display: 'flex', gap: '8px' }}>
            <button
              type="button"
              className="btn btn-secondary compact-btn"
              onClick={() => onStash('save')}
              disabled={isExecuting || totalCount === 0}
              style={{ flex: 1, fontSize: '0.75rem', padding: '4px 8px' }}
              title="Save current uncommitted changes to git stash"
              id="workbenchStashBtn"
            >
              📦 Stash
            </button>
            <button
              type="button"
              className="btn btn-secondary compact-btn"
              onClick={() => onStash('pop')}
              disabled={isExecuting}
              style={{ flex: 1, fontSize: '0.75rem', padding: '4px 8px' }}
              title="Apply most recent stash to working directory"
              id="workbenchPopStashBtn"
            >
              📤 Pop Stash
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
