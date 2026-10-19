import React, { useState, useEffect } from 'react';
import { GitCommitDetailDto } from '../../../../types/git';
import { apiFetch } from '../../../../services/apiClient';
import { Spinner } from '../../../common/Spinner';

export interface GitCommitDetailInspectorProps {
  workspaceId: string;
  commitHash: string;
  onViewDiff: (filePath: string) => void;
  onFileSelect?: (filePath: string) => void;
}

export const GitCommitDetailInspector: React.FC<GitCommitDetailInspectorProps> = ({
  workspaceId,
  commitHash,
  onViewDiff,
  onFileSelect,
}) => {
  const [detail, setDetail] = useState<GitCommitDetailDto | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let isCancelled = false;

    const fetchDetail = async () => {
      setIsLoading(true);
      setError(null);
      try {
        const res = await apiFetch<GitCommitDetailDto>(
          `/api/v1/git/commit-details?workspaceId=${workspaceId}&hash=${commitHash}`
        );
        if (!isCancelled) {
          if (res.ok && res.data) {
            setDetail(res.data);
          } else {
            setError((res.data as any)?.message || res.error || 'Failed to load commit details.');
          }
        }
      } catch (err: any) {
        if (!isCancelled) {
          setError(err?.message || 'Error fetching commit details.');
        }
      } finally {
        if (!isCancelled) {
          setIsLoading(false);
        }
      }
    };

    if (commitHash) {
      fetchDetail();
    }

    return () => {
      isCancelled = true;
    };
  }, [workspaceId, commitHash]);

  if (isLoading) {
    return (
      <div
        className="git-commit-detail-inspector"
        style={{
          display: 'flex',
          justifyContent: 'center',
          alignItems: 'center',
          height: '100%',
          color: 'var(--text-muted)',
          gap: '8px',
          background: 'var(--bg-surface)',
        }}
      >
        <Spinner size={18} isSpinning={true} />
        <span style={{ fontSize: '0.85rem' }}>Loading commit details...</span>
      </div>
    );
  }

  if (error || !detail) {
    return (
      <div
        className="git-commit-detail-inspector"
        style={{
          padding: '24px',
          textAlign: 'center',
          color: 'var(--text-muted)',
          background: 'var(--bg-surface)',
          height: '100%',
        }}
      >
        <p style={{ color: '#f87171', fontSize: '0.9rem', marginBottom: '8px' }}>
          ⚠️ {error || 'Commit details not found.'}
        </p>
        <p style={{ fontSize: '0.8rem' }}>Could not retrieve metadata for commit {commitHash}.</p>
      </div>
    );
  }

  const renderStatusBadge = (status: string) => {
    const s = status.toUpperCase();
    let bg = 'rgba(245, 158, 11, 0.2)';
    let color = '#f59e0b';

    if (s === 'A' || s === 'ADDED') {
      bg = 'rgba(34, 197, 94, 0.2)';
      color = '#4ade80';
    } else if (s === 'D' || s === 'DELETED') {
      bg = 'rgba(248, 113, 113, 0.2)';
      color = '#f87171';
    } else if (s === 'R' || s === 'RENAMED') {
      bg = 'rgba(56, 189, 248, 0.2)';
      color = '#38bdf8';
    }

    return (
      <span
        className="badge"
        style={{
          fontSize: '0.7rem',
          padding: '1px 5px',
          borderRadius: '3px',
          background: bg,
          color,
          fontWeight: 700,
          minWidth: '18px',
          textAlign: 'center',
        }}
      >
        {s.slice(0, 1)}
      </span>
    );
  };

  return (
    <div className="git-commit-detail-inspector">
      {/* Left Pane: Commit Metadata & Full Message */}
      <div className="git-inspector-meta-pane">
        <div>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '4px' }}>
            <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>Commit</span>
            <code
              style={{
                fontFamily: 'monospace',
                fontSize: '0.78rem',
                color: 'var(--accent-primary)',
                background: 'rgba(56, 189, 248, 0.08)',
                padding: '1px 6px',
                borderRadius: '4px',
              }}
              title={detail.hash}
            >
              {detail.shortHash}
            </code>
          </div>
          <div style={{ fontSize: '0.72rem', color: 'var(--text-muted)', fontFamily: 'monospace', wordBreak: 'break-all' }}>
            {detail.hash}
          </div>
        </div>

        {/* Author & Date */}
        <div>
          <div style={{ fontSize: '0.82rem', fontWeight: 600, color: 'var(--text-heading)' }}>
            {detail.author}
          </div>
          <div style={{ fontSize: '0.74rem', color: 'var(--text-muted)' }}>
            {detail.email} • {new Date(detail.date).toLocaleString()}
          </div>
        </div>

        {/* Parents */}
        {detail.parents && detail.parents.length > 0 && (
          <div>
            <span style={{ fontSize: '0.74rem', color: 'var(--text-muted)', display: 'block', marginBottom: '2px' }}>
              Parents:
            </span>
            <div style={{ display: 'flex', gap: '6px', flexWrap: 'wrap' }}>
              {detail.parents.map((p) => (
                <code
                  key={p}
                  style={{
                    fontFamily: 'monospace',
                    fontSize: '0.72rem',
                    color: 'var(--text-muted)',
                    background: 'rgba(255, 255, 255, 0.05)',
                    padding: '1px 5px',
                    borderRadius: '3px',
                  }}
                  title={p}
                >
                  {p.slice(0, 7)}
                </code>
              ))}
            </div>
          </div>
        )}

        {/* Full Message */}
        <div style={{ flex: 1 }}>
          <span style={{ fontSize: '0.74rem', color: 'var(--text-muted)', display: 'block', marginBottom: '4px' }}>
            Message:
          </span>
          <div
            style={{
              fontSize: '0.82rem',
              color: 'var(--text-heading)',
              whiteSpace: 'pre-wrap',
              background: 'var(--bg-surface)',
              padding: '8px 10px',
              borderRadius: '6px',
              border: '1px solid var(--border-color)',
              lineHeight: 1.4,
              maxHeight: '220px',
              overflowY: 'auto',
            }}
          >
            {detail.message}
          </div>
        </div>
      </div>

      {/* Right Pane: Changed Files List with Additions/Deletions */}
      <div className="git-inspector-changes-pane">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '10px' }}>
          <strong style={{ fontSize: '0.88rem', color: 'var(--text-heading)' }}>
            Changed Files ({detail.files.length})
          </strong>
          {(() => {
            const totalAdditions = detail.files.reduce((acc, f) => acc + (f.additions || 0), 0);
            const totalDeletions = detail.files.reduce((acc, f) => acc + (f.deletions || 0), 0);
            return (
              <span style={{ fontSize: '0.78rem', color: 'var(--text-muted)' }}>
                <span style={{ color: '#4ade80', marginRight: '6px' }}>+{totalAdditions}</span>
                <span style={{ color: '#f87171' }}>-{totalDeletions}</span>
              </span>
            );
          })()}
        </div>

        {detail.files.length === 0 ? (
          <div style={{ padding: '24px', textAlign: 'center', color: 'var(--text-muted)', fontSize: '0.82rem' }}>
            No file modifications in this commit.
          </div>
        ) : (
          <div className="git-file-list-group" style={{ flex: 1, overflowY: 'auto' }}>
            {detail.files.map((f) => (
              <div
                key={f.path}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  padding: '5px 10px',
                  borderRadius: '5px',
                  fontSize: '0.8rem',
                  background: 'var(--bg-subtle)',
                  marginBottom: '4px',
                  border: '1px solid var(--border-color)',
                  transition: 'background 0.15s ease',
                }}
              >
                <div
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: '8px',
                    overflow: 'hidden',
                    textOverflow: 'ellipsis',
                    whiteSpace: 'nowrap',
                    flex: 1,
                    minWidth: 0,
                  }}
                >
                  {renderStatusBadge(f.status)}
                  <span
                    onClick={() => onFileSelect?.(f.path)}
                    style={{ cursor: 'pointer', overflow: 'hidden', textOverflow: 'ellipsis', minWidth: 0 }}
                    title={`Click to open in editor: ${f.path}`}
                  >
                    {f.path}
                  </span>
                </div>

                <div style={{ display: 'flex', alignItems: 'center', gap: '8px', flexShrink: 0 }}>
                  <span style={{ fontSize: '0.72rem', fontFamily: 'monospace' }}>
                    {f.additions > 0 && <span style={{ color: '#4ade80', marginRight: '4px' }}>+{f.additions}</span>}
                    {f.deletions > 0 && <span style={{ color: '#f87171' }}>-{f.deletions}</span>}
                  </span>
                  <button
                    type="button"
                    className="btn btn-secondary compact-btn"
                    onClick={() => onViewDiff(f.path)}
                    style={{ fontSize: '0.72rem', padding: '1px 8px' }}
                    title="View file diff in commit"
                  >
                    Diff
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
};
