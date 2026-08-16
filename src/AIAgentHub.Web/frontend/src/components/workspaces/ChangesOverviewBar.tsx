import React from 'react';
import { FileChangeDto, DiffChangeType } from '../../types/diff';

interface ChangesOverviewBarProps {
  fileChanges: FileChangeDto[];
  isOpen: boolean;
  onToggleOpen: () => void;
  onSelectFile: (fileChangeId: string) => void;
  onOpenFullDiff: () => void;
  onAcceptAll: () => Promise<void>;
  onRejectAll: () => Promise<void>;
}

export const ChangesOverviewBar: React.FC<ChangesOverviewBarProps> = ({
  fileChanges,
  isOpen,
  onToggleOpen,
  onSelectFile,
  onOpenFullDiff,
  onAcceptAll,
  onRejectAll,
}) => {
  const count = fileChanges.length;

  const getFileName = (path: string) => {
    const parts = path.replace(/\\/g, '/').split('/');
    return parts[parts.length - 1] || path;
  };

  const getDirName = (path: string) => {
    const parts = path.replace(/\\/g, '/').split('/');
    if (parts.length <= 1) return '';
    return parts.slice(0, -1).join('/');
  };

  const getChangeBadge = (type: DiffChangeType | string) => {
    if (type === DiffChangeType.Created) {
      return <span style={{ color: '#10b981', fontWeight: 600, fontSize: '0.8rem' }}>+ Created</span>;
    }
    if (type === DiffChangeType.Deleted) {
      return <span style={{ color: '#ef4444', fontWeight: 600, fontSize: '0.8rem' }}>- Deleted</span>;
    }
    return <span style={{ color: '#f59e0b', fontWeight: 600, fontSize: '0.8rem' }}>⚬ Modified</span>;
  };

  return (
    <div className="changes-overview-container">
      {/* Collapsed Toolbar State */}
      {!isOpen && (
        <div className="changes-toolbar-collapsed">
          <div className="changes-toolbar-left">
            <button
              type="button"
              className={`changes-icon-btn ${count > 0 ? 'has-changes' : ''}`}
              onClick={onToggleOpen}
              title={`Changes Overview (${count} ${count === 1 ? 'File' : 'Files'} With Changes)`}
            >
              <span style={{ fontSize: '1rem' }}>📄</span>
              {count > 0 && <span className="changes-badge-dot" />}
            </button>
          </div>

          {count > 0 && (
            <div className="changes-toolbar-right">
              <button
                type="button"
                className="changes-review-pill-btn"
                onClick={onToggleOpen}
                title="Review Changed Files"
              >
                <span>{count} {count === 1 ? 'file' : 'files'} changed</span>
                <span className="changes-arrow-icon">&gt;</span>
              </button>
              <button
                type="button"
                className="btn btn-secondary compact-btn"
                onClick={onOpenFullDiff}
                style={{ padding: '3px 8px', fontSize: '0.75rem', display: 'flex', alignItems: 'center', gap: '4px' }}
                title="Open Review Diffs dialog"
              >
                <span>📝 Review Changes</span>
              </button>
            </div>
          )}
        </div>
      )}

      {/* Expanded Antigravity-style File Changes Panel */}
      {isOpen && (
        <div className="changes-panel-expanded glass">
          {/* Header */}
          <div className="changes-panel-header">
            <div className="changes-header-left">
              <button
                type="button"
                className="changes-back-btn"
                onClick={onToggleOpen}
                title="Collapse Changed Files"
              >
                ←
              </button>
              <span style={{ fontSize: '1rem' }}>📄</span>
              <strong style={{ fontSize: '0.88rem', color: 'var(--text-heading)' }}>
                {count} {count === 1 ? 'File' : 'Files'} With Changes
              </strong>
            </div>

            {count > 0 && (
              <div className="changes-header-right">
                <button
                  type="button"
                  className="changes-action-link text-danger"
                  onClick={onRejectAll}
                  title="Rollback all pending file modifications"
                >
                  Reject all
                </button>
                <button
                  type="button"
                  className="btn btn-primary compact-btn"
                  onClick={onAcceptAll}
                  style={{ padding: '3px 10px', fontSize: '0.78rem' }}
                  title="Accept all pending file modifications"
                >
                  Accept all
                </button>
              </div>
            )}
          </div>

          {/* Files List */}
          <div className="changes-files-list">
            {count === 0 ? (
              <div style={{ padding: '12px', textAlign: 'center', color: 'var(--text-muted)', fontSize: '0.84rem' }}>
                No modified files recorded in this conversation yet.
              </div>
            ) : (
              <>
                {fileChanges.slice(0, 8).map((file) => (
                  <div
                    key={file.id}
                    className="changes-file-item"
                    onClick={() => onSelectFile(file.id)}
                    title={`Click to view diff for ${file.relativePath}`}
                  >
                    <div className="changes-file-badge">
                      {getChangeBadge(file.changeType)}
                    </div>
                    <div className="changes-file-name" title={file.relativePath}>
                      <strong>{getFileName(file.relativePath)}</strong>
                      {getDirName(file.relativePath) && (
                        <span className="changes-file-dir">
                          .../{getDirName(file.relativePath)}
                        </span>
                      )}
                    </div>
                    <div className="changes-file-arrow">
                      &rsaquo;
                    </div>
                  </div>
                ))}

                {count > 8 && (
                  <div style={{ padding: '6px 10px', textAlign: 'center', borderTop: '1px solid var(--border-color)' }}>
                    <button
                      type="button"
                      className="changes-view-all-link"
                      onClick={onOpenFullDiff}
                    >
                      View all {count} changed files in full diff reviewer &rarr;
                    </button>
                  </div>
                )}
              </>
            )}
          </div>
        </div>
      )}
    </div>
  );
};
